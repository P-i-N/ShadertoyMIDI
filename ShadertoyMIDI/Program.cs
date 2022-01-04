using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShadertoyMIDI
{
    public class Program
    {
        private List<OPL2Instrument> OPL2InstrumentsBank = new List<OPL2Instrument>();

        private double MaximumNoteReleaseTime { get; set; } = 1.0;

        private struct MergedMidiEvent
        {
            public double TimeBegin;
            public double TimeEnd;
            public int TicksBegin;
            public int TicksEnd;
            public int Program;
            public int Channel;
            public int Note;
            public int Velocity;
            public int Panning;

            public bool IsInTimeRange(double fromTime, double toTime, double releaseTime = 0.0)
            {
                return fromTime <= (TimeEnd + releaseTime) && toTime >= TimeBegin;
            }

            public string ToGLSLVec4String()
            {
                return string.Format("vec4({0}, {1}, {2}, {3})",
                    TimeBegin.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture),
                    TimeEnd.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture),
                    ((double)Program + (double)Panning / 127.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    ((double)Note + ((127 - Velocity) / 127.0)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }

            public static readonly int BitsTicksBegin = 11;
            public static readonly int BitsDuration = 7;
            public static readonly int BitsInstrumentIndex = 3;
            public static readonly int BitsNote = 7;
            public static readonly int BitsVelocity = 3;

            public string ToGLSLUIntString()
            {
                /*
                 * 10bits - ticks begin
                 *  7bits - duration
                 * */
                uint timeInfo = (uint)(TicksBegin & ((1 << BitsTicksBegin) - 1));
                timeInfo |= (uint)((TicksEnd - TicksBegin) & ((1 << BitsDuration) - 1)) << BitsTicksBegin;

                /*
                 *  7bits - note
                 *  3bits - instrument index
                 *  3bits - velocity
                 * */
                uint noteInfo = (uint)(Note & ((1 << BitsNote) - 1));
                noteInfo |= (uint)(Program & ((1 << BitsInstrumentIndex) - 1)) << BitsNote;
                noteInfo |= (uint)((Velocity >> (7 - BitsVelocity)) & ((1 << BitsVelocity) - 1)) << (BitsNote + BitsInstrumentIndex);
                return String.Format("0x{0:X8}u", timeInfo | (noteInfo << (BitsTicksBegin + BitsDuration)));
            }
        }

        private struct MidiChannelState
        {
            public int Program;
            public int Panning;

            public MidiChannelState()
            {
                Program = 0;
                Panning = 64;
            }
        }

        private List<MergedMidiEvent> MergeMidiEvents(MidiFile mf)
        {
            var mergedEvents = new List<MergedMidiEvent>();

            foreach (var track in mf.Tracks)
            {
                var channelStates = new MidiChannelState[16];
                for (int i = 0; i < channelStates.Length; i++)
                    channelStates[i] = new MidiChannelState();

                List<MidiEvent> noteOnEvents = new List<MidiEvent>();

                foreach (var e in track.Events)
                {
                    if (e.Type == MidiEventType.NoteOn)
                        noteOnEvents.Add(e);
                    else if (e.Type == MidiEventType.NoteOff)
                    {
                        var index = noteOnEvents.FindIndex(me => me.Value0 == e.Value0 && me.Channel == e.Channel);
                        if (index >= 0)
                        {
                            // Skip notes for channel 10 (drums)
                            if (e.Channel == 9)
                            {
                                noteOnEvents.RemoveAt(index);
                                continue;
                            }

                            var mme = new MergedMidiEvent();
                            mme.TimeBegin = noteOnEvents[index].Time;
                            mme.TimeEnd = e.Time;
                            mme.TicksBegin = noteOnEvents[index].Ticks;
                            mme.TicksEnd = e.Ticks;
                            mme.Program = channelStates[e.Channel].Program;
                            mme.Channel = e.Channel;
                            mme.Note = e.Value0;
                            mme.Velocity = noteOnEvents[index].Value1;
                            mme.Panning = channelStates[e.Channel].Panning;

                            if (mme.TicksBegin == mme.TicksEnd)
                                mme.TicksEnd += 1;

                            noteOnEvents.RemoveAt(index);
                            mergedEvents.Add(mme);
                        }
                    }
                    else if (e.Type == MidiEventType.ProgramChange)
                        channelStates[e.Channel].Program = e.Value0;
                    else if (e.Type == MidiEventType.ControlChange)
                    {
                        if (e.Value0 == 10) // Panning
                            channelStates[e.Channel].Panning = e.Value1;
                    }
                }
            }

            mergedEvents.Sort((a, b) => Math.Sign(a.TimeBegin - b.TimeBegin));
            return mergedEvents;
        }

        private Dictionary<int, int> RemapMidiPrograms(List<MergedMidiEvent> mergedMidiEvents)
        {
            var programMappings = new Dictionary<int, int>();

            for (int i = 0; i < mergedMidiEvents.Count; ++i)
            {
                var mme = mergedMidiEvents[i];

                if (!programMappings.ContainsKey(mme.Program))
                    programMappings.Add(mme.Program, programMappings.Count);

                mme.Program = programMappings[mme.Program];
                mergedMidiEvents[i] = mme;
            }

            return programMappings;
        }

        private IEnumerable<MergedMidiEvent> FilterTimeRange(IEnumerable<MergedMidiEvent> mergedMidiEvents, double fromTime, double toTime)
        {
            return mergedMidiEvents.Where(mme => mme.IsInTimeRange(fromTime, toTime, MaximumNoteReleaseTime));
        }

        private List<Tuple<int, int>> BuildTimeEventRanges(IEnumerable<MergedMidiEvent> mergedMidiEvents, double rangeLength = 1.0)
        {
            var timeEventRanges = new List<Tuple<int, int>>();

            if (!mergedMidiEvents.Any())
                return timeEventRanges;

            double minTime = mergedMidiEvents.First().TimeBegin;
            double maxTime = minTime;

            foreach (var mme in mergedMidiEvents)
            {
                if (mme.TimeEnd + MaximumNoteReleaseTime > maxTime)
                    maxTime = mme.TimeEnd + MaximumNoteReleaseTime;
            }

            int numRanges = (int)Math.Ceiling(maxTime / rangeLength);
            for (int i = 0; i < numRanges; ++i)
            {
                double fromTime = i * rangeLength;
                double toTime = (i + 1) * rangeLength;

                int j = 0;
                int minIndex = int.MaxValue;
                int maxIndex = int.MinValue;

                foreach (var mme in mergedMidiEvents)
                {
                    if (mme.IsInTimeRange(fromTime, toTime, MaximumNoteReleaseTime))
                    {
                        minIndex = Math.Min(j, minIndex);
                        maxIndex = Math.Max(j + 1, maxIndex);
                    }

                    ++j;
                }

                if (minIndex < maxIndex)
                    timeEventRanges.Add(new Tuple<int, int>(minIndex, maxIndex));
                else
                    timeEventRanges.Add(new Tuple<int, int>(0, 0));
            }

            return timeEventRanges;
        }

        private void ApplyTimeOffset(List<MergedMidiEvent> mergedMidiEvents, double timeOffset, int ticksOffset)
        {
            for (int i = 0; i < mergedMidiEvents.Count; ++i)
            {
                var mme = mergedMidiEvents[i];
                mme.TimeBegin += timeOffset;
                mme.TimeEnd += timeOffset;
                mme.TicksBegin += ticksOffset;
                mme.TicksEnd += ticksOffset;

                mergedMidiEvents[i] = mme;
            }
        }

        private void WriteGLSLArray(TextWriter tw, string arrayType, string arrayName, int arraySize, Func<int, string> itemCallback, int itemsPerLine)
        {
            tw.WriteLine("const {0} {1}[{2}] = {0}[](", arrayType, arrayName, arraySize);
            tw.Write("    ");

            for (int i = 0; i < arraySize; ++i)
            {
                tw.Write("{0}{1}", itemCallback(i), (i < arraySize - 1) ? ", " : "");

                if (((i + 1) % itemsPerLine) == 0)
                {
                    if (i < arraySize - 1)
                    {
                        tw.WriteLine();
                        tw.Write("    ");
                    }
                    else
                        tw.Write(" ");
                }
            }

            tw.WriteLine(" );");
        }

        private void WriteGLSLArray(TextWriter tw, string arrayName, List<MergedMidiEvent> mergedMidiEvents, int eventsPerLine = 5)
        {
            WriteGLSLArray(
                tw,
                "vec4",
                arrayName,
                mergedMidiEvents.Count,
                i => mergedMidiEvents[i].ToGLSLVec4String(),
                eventsPerLine);
        }

        private void WriteGLSLUIntArray(TextWriter tw, string arrayName, List<MergedMidiEvent> mergedMidiEvents, int eventsPerLine = 5)
        {
            WriteGLSLArray(
                tw,
                "uint",
                arrayName,
                mergedMidiEvents.Count,
                i => mergedMidiEvents[i].ToGLSLUIntString(),
                eventsPerLine);
        }

        private void WriteGLSLArray(TextWriter tw, string arrayName, List<Tuple<int, int>> timeEventRanges, int eventsPerLine = 8)
        {
            WriteGLSLArray(
                tw,
                "ivec2",
                arrayName,
                timeEventRanges.Count,
                i => string.Format("ivec2({0}, {1})", timeEventRanges[i].Item1, timeEventRanges[i].Item2),
                eventsPerLine);
        }

        private void ConvertMIDI(string inputFileName, string outputFileName)
        {
            Console.WriteLine("Converting {0} into {1}:", inputFileName, outputFileName);

            var sw = new StringWriter();
            var br = new BinaryReader(new FileStream(inputFileName, FileMode.Open));

            var mf = MidiFile.Parse(br);

            var mergedEvents = MergeMidiEvents(mf);
            Console.WriteLine("Last event end tick: {0}", mergedEvents.Last().TicksEnd);

            var maxTicksDuration = mergedEvents.MaxBy(mme => mme.TicksEnd - mme.TicksBegin);
            Console.WriteLine("Max ticks duration: {0}", maxTicksDuration.TicksEnd - maxTicksDuration.TicksBegin);

            var firstEventTime = mergedEvents[0].TimeBegin;
            var firstEventTicks = mergedEvents[0].TicksBegin;
            
            mergedEvents = FilterTimeRange(mergedEvents, firstEventTime, firstEventTime + 120.0).ToList();
            ApplyTimeOffset(mergedEvents, -firstEventTime, -firstEventTicks);

            var timeEventRanges = BuildTimeEventRanges(mergedEvents);

            var programMappings = RemapMidiPrograms(mergedEvents);

            var programMappingList = new List<int>();
            for (int i = 0; i < programMappings.Count; ++i)
                programMappingList.Add(0);

            foreach (var kvp in programMappings)
                programMappingList[kvp.Value] = kvp.Key;

            sw.WriteLine("struct OPL2Operator { vec4 params, adsr; };");
            sw.WriteLine("struct OPL2Voice { OPL2Operator mod, car; int feedback, noteOffset; };");

            sw.WriteLine("struct OPL2Instrument\n{");
            sw.WriteLine("    vec4 mod0Params, mod0ADSR, car0Params, car0ADSR;");
            sw.WriteLine("    vec4 mod1Params, mod1ADSR, car1Params, car1ADSR;");
            sw.WriteLine("};\n");

            sw.WriteLine("const OPL2Instrument opl2instruments[{0}] = OPL2Instrument[](", programMappings.Count);
            for (int i = 0; i < programMappingList.Count; ++i)
            {
                var gmIndex = programMappingList[i];

                OPL2Instrument instr = (gmIndex < OPL2InstrumentsBank.Count)
                    ? OPL2InstrumentsBank[gmIndex]
                    : OPL2InstrumentsBank[0];

                if (i > 0)
                    sw.WriteLine();

                sw.WriteLine("    // {0}: \"{1}\" (program {2})", i, instr.Name, programMappingList[i]);
                sw.Write("    {0}", instr.ToGLSLString());

                if (i < programMappingList.Count - 1)
                    sw.WriteLine(",");
                else
                    sw.WriteLine();
            }
            sw.WriteLine(");\n");

            sw.WriteLine("struct Instrument { vec4 oscilators, octaves, adsr, fx; };\n");
            sw.WriteLine("const Instrument instruments[{0}] = Instrument[](", programMappings.Count);

            for (int i = 0; i < programMappingList.Count; ++i)
            {
                if (i > 0)
                    sw.WriteLine();

                sw.WriteLine("    // {0}: \"{1}\" (program {2})",
                i, MidiInstrument.Names[programMappingList[i]], programMappingList[i]);

                var instr = MidiInstrument.Create(programMappingList[i]);

                // Determine average panning
                {
                    int avgPanning = 0;
                    int count = 0;

                    foreach (var mme in mergedEvents)
                    {
                        if (mme.Program == i)
                        {
                            avgPanning += mme.Panning;
                            ++count;
                        }
                    }

                    if (count > 0)
                        avgPanning /= count;
                    else
                        avgPanning = 64;

                    instr.FX[1] = (float)avgPanning / 127.0f;
                }

                sw.Write("    Instrument( vec4({0}, {1}, {2}, {3}), vec4({4}, {5}, {6}, {7}), vec4({8}, {9}, {10}, {11}), vec4({12}, {13}, {14}, {15}) )",
                    instr.Oscilators[0].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.Oscilators[1].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.Oscilators[2].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.Oscilators[3].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.Octaves[0].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.Octaves[1].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.Octaves[2].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.Octaves[3].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.ADSR[0].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.ADSR[1].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.ADSR[2].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.ADSR[3].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.FX[0].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.FX[1].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.FX[2].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    instr.FX[3].ToString(System.Globalization.CultureInfo.InvariantCulture));

                if (i < programMappingList.Count - 1)
                    sw.WriteLine(",");
                else
                    sw.WriteLine();
            }

            sw.WriteLine(");\n");

            WriteGLSLUIntArray(sw, "noteEvents", mergedEvents, 15);
            sw.WriteLine();

            sw.WriteLine("// First usable noteEvent index for every second");
            WriteGLSLArray(sw, "timeEventRanges", timeEventRanges);
            sw.WriteLine();

            sw.WriteLine("const float secsPerTick = {0};",
                ((mf.Tempo / 1000000.0) / (double)mf.TicksPerQuarterNote).ToString(System.Globalization.CultureInfo.InvariantCulture));

            sw.WriteLine("const float ticksPerSec = {0};\n",
                ((double)mf.TicksPerQuarterNote / (mf.Tempo / 1000000.0)).ToString(System.Globalization.CultureInfo.InvariantCulture));

            sw.WriteLine("void FillNoteEvent(in int i, out uvec4 e)\n{");
            sw.WriteLine("    uint n = noteEvents[i];\n");
            sw.WriteLine("    // {0} bits for note begin in ticks", MergedMidiEvent.BitsTicksBegin);
            sw.WriteLine("    e.x = n & {0}u; n = n >> {1};\n", (1 << MergedMidiEvent.BitsTicksBegin) - 1, MergedMidiEvent.BitsTicksBegin);
            sw.WriteLine("    // {0} bits for note duration in ticks", MergedMidiEvent.BitsDuration);
            sw.WriteLine("    e.y = e.x + (n & {0}u); n = n >> {1};\n", (1 << MergedMidiEvent.BitsDuration) - 1, MergedMidiEvent.BitsDuration);
            sw.WriteLine("    // {0} bits for note", MergedMidiEvent.BitsNote);
            sw.WriteLine("    e.z = n & {0}u; n = n >> {1};\n", (1 << MergedMidiEvent.BitsNote) - 1, MergedMidiEvent.BitsNote);
            sw.WriteLine("    // {0} bits for instrument index with {1} bits for velocity", MergedMidiEvent.BitsInstrumentIndex, MergedMidiEvent.BitsVelocity);
            sw.WriteLine("    e.w = (n & {0}u) | ((n & ~{0}u) << {1});", (1 << MergedMidiEvent.BitsInstrumentIndex) - 1, 4 - MergedMidiEvent.BitsInstrumentIndex);
            sw.WriteLine("}");

            File.WriteAllText(outputFileName, sw.ToString());

            Console.WriteLine();
        }

        private int Run(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No input files specified!");
                return -1;
            }

            try
            {
                OPL2InstrumentsBank = OPL2Instrument.LoadBankFromFile("GENMIDI.op2");
            }
            catch (Exception)
            {
                Console.WriteLine("Could not load GENMIDI.op2!");
                OPL2InstrumentsBank = new List<OPL2Instrument>();
            }

            foreach (var arg in args)
            {
                var dir = Path.GetDirectoryName(arg);
                if (string.IsNullOrEmpty(dir))
                    dir = ".";

                var fileName = Path.GetFileName(arg);
                var files = Directory.GetFiles(dir, fileName);

                foreach (var f in files)
                {
                    try
                    {
                        ConvertMIDI(f, Path.ChangeExtension(f, "glsl"));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            }

            return 0;
        }

        public static int Main(string[] args)
        {
            var p = new Program();
            return p.Run(args);
        }
    }
}
