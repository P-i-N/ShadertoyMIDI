using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadertoyMIDI
{
    public struct OPL2Operator
    {
        public int Characteristic;
        public int Attack;
        public int Decay;
        public int Sustain;
        public int Release;
        public int WaveSelect;
        public int Scale;
        public int Level;

        static internal OPL2Operator Create(BinaryReader br)
        {
            var result = new OPL2Operator();

            result.Characteristic = br.ReadByte();
            result.Attack = br.ReadByte();
            result.Decay = result.Attack & 0x0F;
            result.Attack >>= 4;

            result.Sustain = br.ReadByte();
            result.Release = result.Sustain & 0x0F;
            result.Sustain >>= 4;

            result.WaveSelect = br.ReadByte();
            result.Scale = br.ReadByte();
            result.Level = br.ReadByte();

            return result;
        }
    }

    public struct OPL2Voice
    {
        public OPL2Operator Modulator;
        public int Feedback;
        public OPL2Operator Carrier;
        public int NoteOffset;

        static internal OPL2Voice Create(BinaryReader br)
        {
            var result = new OPL2Voice();

            result.Modulator = OPL2Operator.Create(br);
            result.Feedback = br.ReadByte();

            result.Carrier = OPL2Operator.Create(br);
            br.ReadByte(); // Reserved, unused
            result.NoteOffset = br.ReadInt16();

            return result;
        }
    }

    public class OPL2Instrument
    {
        public int BankIndex { get; set; } = -1;

        public string Name { get; set; } = string.Empty;

        public int Flags { get; set; }

        public int FineTune { get; set; }

        public int NoteNumber { get; set; }

        public OPL2Voice[] Voices { get; set; } = new OPL2Voice[2];

        public string ToGLSLString()
        {
            string result = "OPL2Instrument(";

            for (int i = 0; i < 2; ++i)
            {
                var v = Voices[i];

                result += string.Format("vec4({0}, {1}, {2}, {3}), ",
                    v.Modulator.Characteristic,
                    v.Modulator.WaveSelect,
                    v.Modulator.Scale,
                    v.Modulator.Level);

                result += string.Format("vec4({0}, {1}, {2}, {3}), ",
                    v.Modulator.Attack,
                    v.Modulator.Decay,
                    v.Modulator.Sustain,
                    v.Modulator.Release);

                result += string.Format("vec4({0}, {1}, {2}, {3}), ",
                    v.Carrier.Characteristic,
                    v.Carrier.WaveSelect,
                    v.Carrier.Scale,
                    v.Carrier.Level);

                result += string.Format("vec4({0}, {1}, {2}, {3})",
                    v.Carrier.Attack,
                    v.Carrier.Decay,
                    v.Carrier.Sustain,
                    v.Carrier.Release);

                if (i == 0)
                    result += ", ";
            }

            return result + ")";
        }

        public static OPL2Instrument ParseFromBank(int bankIndex, BinaryReader br)
        {
            OPL2Instrument result = new OPL2Instrument();

            result.BankIndex = bankIndex;
            result.Flags = br.ReadUInt16();
            result.FineTune = br.ReadByte();
            result.NoteNumber = br.ReadByte();

            for (int i = 0; i < 2; ++i)
                result.Voices[i] = OPL2Voice.Create(br);

            return result;
        }

        public static List<OPL2Instrument> LoadBankFromFile(string fileName)
        {
            var result = new List<OPL2Instrument>();

            var br = new BinaryReader(new FileStream(fileName, FileMode.Open));

            var header = br.ReadChars(8);
            if (!header.SequenceEqual("#OPL_II#"))
                throw new InvalidDataException("Invalid OP2 header!");

            for (int i = 0; i < 175; ++i)
            {
                var instr = ParseFromBank(i, br);
                result.Add(instr);
            }

            for (int i = 0; i < 175; ++i)
            {
                var nameBytes = br.ReadBytes(32);

                for (int j = 0; j < nameBytes.Length && (nameBytes[j] >= 32); ++j)
                    result[i].Name += char.ConvertFromUtf32(nameBytes[j]);

                result[i].Name = result[i].Name.Trim();

                if (string.IsNullOrWhiteSpace(result[i].Name) && i < MidiInstrument.Names.Length)
                    result[i].Name = MidiInstrument.Names[i];
            }

            return result;
        }
    }
}
