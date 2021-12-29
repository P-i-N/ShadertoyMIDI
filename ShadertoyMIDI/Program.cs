using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShadertoyMIDI
{
    public class Program
    {
        private struct MergedMidiEvent
        {
            public double TimeBegin;
            public double TimeEnd;
            public int Program;
            public int Channel;
            public int Note;
            public int Velocity;
            public int Panning;
        }

        private struct MidiChannelState
        {
            public int Program;
            public int Panning;
            public int EventCount;

            public MidiChannelState()
            {
                Program = 0;
                Panning = 64;
                EventCount = 0;
            }
        }

        private void ConvertMIDI(string inputFileName, string outputFileName)
        {
            Console.WriteLine("Converting {0} into {1}:", inputFileName, outputFileName);

            var sw = new StringWriter();
            var br = new BinaryReader(new FileStream(inputFileName, FileMode.Open));

            var mf = MidiFile.Parse(br);

            var mergedEvents = new List<MergedMidiEvent>();
            var timeEventRanges = new List<Tuple<int, int>>();
            var programMappings = new Dictionary<int, int>();

            foreach (var track in mf.Tracks)
            {
                Console.WriteLine("- track {0}: {1}", track.Index, track.Name);

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
                            // Skip notes for program 0 (usually drums)
                            if (channelStates[e.Channel].Program == 0)
                            {
                                noteOnEvents.RemoveAt(index);
                                continue;
                            }

                            if (!programMappings.ContainsKey(channelStates[e.Channel].Program))
                                programMappings.Add(channelStates[e.Channel].Program, programMappings.Count);

                            var mme = new MergedMidiEvent();
                            mme.TimeBegin = noteOnEvents[index].Time;
                            mme.TimeEnd = e.Time;
                            mme.Program = programMappings[channelStates[e.Channel].Program];
                            mme.Channel = e.Channel;
                            mme.Note = e.Value0;
                            mme.Velocity = noteOnEvents[index].Value1;
                            mme.Panning = channelStates[e.Channel].Panning;

                            noteOnEvents.RemoveAt(index);
                            mergedEvents.Add(mme);

                            channelStates[e.Channel].EventCount += 1;
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

            double songLength = 0.0;
            foreach (var mme in mergedEvents)
                if (mme.TimeEnd > songLength)
                    songLength = mme.TimeEnd;

            songLength += 2.0;

            sw.WriteLine("struct Instrument { vec4 oscilators, octaves, adsr, fx; };\n");
            sw.WriteLine("const Instrument instruments[{0}] = Instrument[](", programMappings.Count);

            var programMappingList = new List<int>();
            for (int i = 0; i < programMappings.Count; ++i)
                programMappingList.Add(0);

            foreach (var kvp in programMappings)
                programMappingList[kvp.Value] = kvp.Key;

            for (int i = 0; i < programMappingList.Count; ++i)
            {
                if (i > 0)
                    sw.WriteLine();

                sw.WriteLine("    // {0}: \"{1}\" (program {2})",
                i, MidiInstrument.Names[programMappingList[i]], programMappingList[i]);

                var instr = MidiInstrument.Create(programMappingList[i]);
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

            sw.WriteLine("// Tuples of: [time begin, time end, program + panning, note + invVelocity]...");
            sw.WriteLine("const vec4 noteEvents[{0}] = vec4[](", mergedEvents.Count);
            sw.Write("    ");

            for (int i = 0; i <= (int)songLength; ++i)
                timeEventRanges.Add(new Tuple<int, int>(int.MaxValue, int.MinValue));

            for (int i = 0; i < mergedEvents.Count; ++i)
            {
                var mme = mergedEvents[i];

                sw.Write("vec4({0}, {1}, {2}, {3}){4}",
                    mme.TimeBegin.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                    mme.TimeEnd.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                    ((double)mme.Program + (double)mme.Panning / 127.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    ((double)mme.Note + ((127 - mme.Velocity) / 127.0)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    (i < mergedEvents.Count - 1) ? ", " : "");

                // Update time event ranges
                {
                    int secondsStart = (int)Math.Floor(mme.TimeBegin);
                    int secondsEnd = (int)Math.Ceiling(mme.TimeEnd + 1.0);

                    for (int s = secondsStart; s <= secondsEnd; ++s)
                    {
                        timeEventRanges[s] = new Tuple<int, int>(
                            Math.Min(i, timeEventRanges[s].Item1),
                            Math.Max(i, timeEventRanges[s].Item2));
                    }
                }

                if (((i + 1) % 5) == 0)
                {
                    if (i < mergedEvents.Count - 1)
                    {
                        sw.WriteLine();
                        sw.Write("    ");
                    }
                    else
                        sw.Write(" ");
                }
            }

            sw.WriteLine(" );");
            sw.WriteLine();

            sw.WriteLine("// First usable noteEvent index for every second");
            sw.WriteLine("const ivec2 timeEventRanges[{0}] = ivec2[](", timeEventRanges.Count);
            sw.Write("    ");

            for (int i = 0; i < timeEventRanges.Count; ++i)
            {
                var minValue = timeEventRanges[i].Item1 != int.MaxValue ? timeEventRanges[i].Item1 : 0;
                var maxValue = timeEventRanges[i].Item2 != int.MinValue ? (timeEventRanges[i].Item2 + 1) : 0;

                sw.Write("ivec2({0}, {1}){2}",
                    minValue,
                    maxValue,
                    (i < timeEventRanges.Count - 1) ? ", " : "");

                if (((i + 1) % 30) == 0)
                {
                    if (i < timeEventRanges.Count - 1)
                    {
                        sw.WriteLine();
                        sw.Write("    ");
                    }
                    else
                        sw.Write(" ");
                }
            }

            sw.WriteLine(" );");
            sw.WriteLine();

            sw.WriteLine("const int songLengthSeconds = {0};", (int)songLength);

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

            foreach (var arg in args)
                ConvertMIDI(arg, Path.ChangeExtension(arg, "glsl"));

            return 0;
        }

        public static int Main(string[] args)
        {
            var p = new Program();
            return p.Run(args);
        }
    }
}
