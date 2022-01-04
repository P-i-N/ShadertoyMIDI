using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadertoyMIDI
{
    public enum MidiEventType
    {
        NoteOff,
        NoteOn,
        KeyAfterTouch,
        ControlChange,
        ProgramChange,
        ChannelAfterTouch,
        PitchWheelChange,
        Tempo,
    }

    public struct MidiEvent
    {
        public int Track;
        public double Time;
        public int Ticks;
        public int Channel;
        public MidiEventType Type;
        public int Value0;
        public int Value1;
    }

    public class MidiTrack
    {
        public int Index { get; set; } = 0;

        public string Name { get; set; } = String.Empty;

        public List<MidiEvent> Events { get; } = new List<MidiEvent>();

        private static int ReadVarLengthInt(BinaryReader br)
        {
            var valueByte = br.ReadByte();
            int result = valueByte & 0x7F;

            if ((valueByte & 0x80) != 0)
            {
                valueByte = br.ReadByte();
                result <<= 7;
                result += (valueByte & 0x7F);
                if ((valueByte & 0x80) != 0)
                {
                    valueByte = br.ReadByte();
                    result <<= 7;
                    result += (valueByte & 0x7F);
                    if ((valueByte & 0x80) != 0)
                    {
                        valueByte = br.ReadByte();
                        result <<= 7;
                        result += (valueByte & 0x7F);
                        if ((valueByte & 0x80) != 0)
                        {
                            valueByte = br.ReadByte();
                            result <<= 7;
                            result += (valueByte & 0x7F);
                            if ((valueByte & 0x80) != 0)
                                throw new InvalidDataException("Variable length value too big!");
                        }
                    }
                }
            }

            return result;
        }

        public static MidiTrack Parse(BinaryReader br, int trackIndex = 0)
        {
            var result = new MidiTrack();
            result.Index = trackIndex;

            if (!br.ReadChars(4).SequenceEqual("MTrk"))
                throw new InvalidDataException("Invalid MIDI track header!");

            var trackLength = BitUtils.ConvertToLittleEndian(br.ReadUInt32());
            var endPosition = br.BaseStream.Position + trackLength;

            int timeTicks = 0;
            byte prevEventType = 0;

            while (br.BaseStream.Position < endPosition)
            {
                int delta = ReadVarLengthInt(br);
                timeTicks += delta;

                var e = new MidiEvent();
                e.Track = trackIndex;
                e.Ticks = timeTicks;

                var eventType = br.ReadByte();

                if (eventType < 128)
                {
                    br.BaseStream.Position -= 1;
                    eventType = prevEventType;
                }

                prevEventType = eventType;

                if ((eventType & 0xF0) == 0x80) // Note off
                {
                    e.Channel = eventType & 0x0F;
                    e.Type = MidiEventType.NoteOff;
                    e.Value0 = br.ReadByte();
                    e.Value1 = br.ReadByte();
                    result.Events.Add(e);
                }
                else if ((eventType & 0xF0) == 0x90) // Note on
                {
                    e.Channel = eventType & 0x0F;
                    e.Type = MidiEventType.NoteOn;
                    e.Value0 = br.ReadByte();
                    e.Value1 = br.ReadByte();

                    if (e.Value1 == 0)
                        e.Type = MidiEventType.NoteOff;

                    result.Events.Add(e);
                }
                else if ((eventType & 0xF0) == 0xB0) // Control change
                {
                    e.Channel = eventType & 0x0F;
                    e.Type = MidiEventType.ControlChange;
                    e.Value0 = br.ReadByte();
                    e.Value1 = br.ReadByte();
                    result.Events.Add(e);
                }
                else if ((eventType & 0xF0) == 0xC0) // Program change
                {
                    e.Channel = eventType & 0x0F;
                    e.Type = MidiEventType.ProgramChange;
                    e.Value0 = br.ReadByte();
                    result.Events.Add(e);
                }
                else if ((eventType & 0xF0) == 0xE0) // Pitch wheel change
                {
                    e.Channel = eventType & 0x0F;
                    e.Type = MidiEventType.PitchWheelChange;
                    e.Value0 = br.ReadByte();
                    e.Value1 = br.ReadByte();
                    result.Events.Add(e);
                }
                else if (eventType == 0xF0) // System exclusive
                {
                    var sysExclLength = ReadVarLengthInt(br);
                    br.ReadBytes(sysExclLength);
                }
                else if (eventType == 0xFF) // Meta event
                {
                    var metaEventType = br.ReadByte();
                    var metaEventLength = ReadVarLengthInt(br);
                    var metaBytes = br.ReadBytes(metaEventLength);

                    if (metaEventType == 3) // Track name
                        result.Name = Encoding.ASCII.GetString(metaBytes);
                    else if (metaEventType == 81) // Tempo
                    {
                        e.Channel = 0;
                        e.Type = MidiEventType.Tempo;
                        e.Value0 = metaBytes[2] + metaBytes[1] * 256 + metaBytes[0] * 65536;
                        result.Events.Add(e);
                    }
                }
                else
                    throw new Exception("Unknown MIDI event type!");
            }

            return result;
        }
    }

    public class MidiFile
    {
        public int TicksPerQuarterNote { get; private set; } = 0;

        public int Tempo { get; private set; } = 0;

        public List<MidiTrack> Tracks { get; private set; } = new List<MidiTrack>();

        private void QuantizeTicks(int quarterNoteSubdiv)
        {
            foreach (var track in Tracks)
            {
                for (int i = 0; i < track.Events.Count; ++i)
                {
                    var e = track.Events[i];

                    var ticksAlign = e.Ticks % quarterNoteSubdiv;
                    if (ticksAlign < quarterNoteSubdiv / 2)
                        e.Ticks -= ticksAlign;
                    else
                        e.Ticks += quarterNoteSubdiv - ticksAlign;

                    e.Ticks /= quarterNoteSubdiv;
                    track.Events[i] = e;
                }
            }

            Tempo *= quarterNoteSubdiv;
            TicksPerQuarterNote = TicksPerQuarterNote / quarterNoteSubdiv;
        }

        private void ResolveEventsRangeTempo(int ticksFrom, int ticksTo, int tempo)
        {
            // tempo = microseconds per quarter note
            double secsPerQuarterNote = tempo / 1000000.0;

            foreach (var track in Tracks)
            {
                for (int i = 0; i < track.Events.Count; ++i)
                {
                    var e = track.Events[i];
                    if (e.Ticks < ticksFrom || e.Ticks >= ticksTo)
                        continue;

                    e.Time = ((double)e.Ticks / TicksPerQuarterNote) * secsPerQuarterNote;
                    track.Events[i] = e;
                }
            }
        }

        private void ResolveEventsTiming()
        {
            foreach (var track in Tracks)
            {
                int tempoTicks = -1;

                for (int i = 0; i < track.Events.Count; ++i)
                {
                    var e = track.Events[i];
                    if (e.Type == MidiEventType.Tempo)
                    {
                        if (Tempo != 0.0)
                            Console.WriteLine("Song has multiple tempos specified: previous = {0}, new = {1}", Tempo, e.Value0);
                        else
                        {
                            Tempo = e.Value0;
                            tempoTicks = e.Ticks;
                        }
                    }
                }

                if (Tempo > 0 && tempoTicks >= 0)
                    ResolveEventsRangeTempo(tempoTicks, int.MaxValue, Tempo);
            }
        }

        public static MidiFile Parse(BinaryReader br)
        {
            MidiFile result = new MidiFile();

            if (!br.ReadChars(4).SequenceEqual("MThd"))
                throw new InvalidDataException("Invalid MIDI header!");

            var headerLength = BitUtils.ConvertToLittleEndian(br.ReadUInt32());
            if (headerLength != 6)
                throw new InvalidDataException("Invalid MIDI header length!");

            var trackFileFormat = BitUtils.ConvertToLittleEndian(br.ReadUInt16());
            var numTracks = BitUtils.ConvertToLittleEndian(br.ReadUInt16());

            result.TicksPerQuarterNote = BitUtils.ConvertToLittleEndian(br.ReadInt16());

            for (int i = 0; i < numTracks; ++i)
            {
                var track = MidiTrack.Parse(br, i);
                if (track.Events.Count > 0)
                    result.Tracks.Add(track);
            }

            result.QuantizeTicks(result.TicksPerQuarterNote / 8);
            result.ResolveEventsTiming();
            return result;
        }
    }
}
