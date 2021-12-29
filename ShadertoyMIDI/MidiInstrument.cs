using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadertoyMIDI
{
    public class MidiInstrument
    {
        public float[] Oscilators = new float[] { 0.5f, 0.0f, 0.0f, 0.0f };
        public float[] Octaves = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float[] ADSR = new float[] { 0.005f, 0.005f, 0.5f, 0.1f };
        public float[] FX = new float[] { 0.0f, 0.0f, 0.0f, 0.0f };

        public MidiInstrument()
        {

        }

        public MidiInstrument(
            float osc0, float osc1, float osc2, float osc3,
            float oct0, float oct1, float oct2, float oct3,
            float a, float d, float s, float r,
            float fx0, float fx1, float fx2, float fx3)
        {
            Oscilators[0] = osc0;
            Oscilators[1] = osc1;
            Oscilators[2] = osc2;
            Oscilators[3] = osc3;

            Octaves[0] = oct0;
            Octaves[1] = oct1;
            Octaves[2] = oct2;
            Octaves[3] = oct3;

            ADSR[0] = a;
            ADSR[1] = d;
            ADSR[2] = s;
            ADSR[3] = r;

            FX[0] = fx0;
            FX[1] = fx1;
            FX[2] = fx2;
            FX[3] = fx3;
        }

        public static MidiInstrument Create(int generalMidiProgram)
        {
            if (generalMidiProgram >= 0 && generalMidiProgram < 8) // Piano
            {

            }
            else if (generalMidiProgram >= 8 && generalMidiProgram < 16) // Chromatic Percussion
            {

            }
            else if (generalMidiProgram >= 16 && generalMidiProgram < 24) // Organ
            {

            }
            else if (generalMidiProgram >= 24 && generalMidiProgram < 32) // Guitar
            {
                return new MidiInstrument(
                    0.99f, 0.5f, 1.5f, 0.0f,
                    2.0f, 1.001f, 1.0f, 1.0f,
                    0.001f, 0.001f, 0.1f, 0.2f,
                    0.0f, 0.0f, 0.0f, 0.0f);
            }
            else if (generalMidiProgram >= 32 && generalMidiProgram < 40) // Bass
            {
                return new MidiInstrument(
                    0.99f, 0.5f, 0.0f, 0.0f,
                    1.0f, 2.0f, 1.0f, 1.0f,
                    0.002f, 0.001f, 0.75f, 0.1f,
                    0.0f, 0.0f, 0.0f, 0.0f);
            }
            else if (generalMidiProgram >= 40 && generalMidiProgram < 48) // Strings
            {
                if (generalMidiProgram == 46) // Orchestral Harp
                {
                    return new MidiInstrument(
                        1.25f, 1.0625f, 0.0f, 0.0f,
                        1.0f, 2.00001f, 1.001f, 1.0f,
                        0.001f, 0.01f, 0.75f, 0.5f,
                        0.0f, 0.0f, 0.0f, 0.0f);
                }

                return new MidiInstrument(
                    0.125f, 1.0625f, 0.0f, 0.0f,
                    1.0f, 2.00001f, 1.001f, 1.0f,
                    0.5f, 0.5f, 0.75f, 0.5f,
                    0.0f, 0.0f, 0.0f, 0.0f);
            }
            else if (generalMidiProgram >= 48 && generalMidiProgram < 56) // Ensemble
            {
                return new MidiInstrument(
                    0.125f, 1.0625f, 0.0f, 0.0f,
                    1.0f, 2.00001f, 1.001f, 1.0f,
                    0.5f, 0.5f, 0.75f, 0.5f,
                    0.0f, 0.0f, 0.0f, 0.0f);
            }
            else if (generalMidiProgram >= 56 && generalMidiProgram < 64) // Brass
            {

            }
            else if (generalMidiProgram >= 64 && generalMidiProgram < 72) // Reed
            {
                if (generalMidiProgram == 66) // Tenor Sax
                {
                    return new MidiInstrument(
                        1.5f, 1.25f, 2.25f, 0.0f,
                        1.0f, 0.5f, 0.501f, 1.0f,
                        0.01f, 0.01f, 0.5f, 0.1f,
                        0.025f, 0.0f, 0.0f, 0.0f);
                }

                return new MidiInstrument(
                    0.5f, 1.25f, 2.25f, 0.0f,
                    1.0f, 1.0f, 1.001f, 1.0f,
                    0.001f, 0.01f, 0.5f, 0.05f,
                    0.02f, 0.0f, 0.0f, 0.0f);
            }
            else if (generalMidiProgram >= 72 && generalMidiProgram < 80) // Pipe
            {

            }

            Console.WriteLine("Unknown instrument {0}, using generic!", generalMidiProgram);
            return new MidiInstrument();
        }

        public static string[] Names = new string[]
        {
            "Acoustic Grand Piano",
            "Bright Acoustic Piano",
            "Electric Grand Piano",
            "Honky-tonk Piano",
            "Electric Piano 1",
            "Electric Piano 2",
            "Harpsichord",
            "Clavi",
            "Celesta",
            "Glockenspiel",
            "Music Box",
            "Vibraphone",
            "Marimba",
            "Xylophone",
            "Tubular Bells",
            "Dulcimer",
            "Drawbar Organ",
            "Percussive Organ",
            "Rock Organ",
            "Church Organ",
            "Reed Organ",
            "Accordion",
            "Harmonica",
            "Tango Accordion",
            "Acoustic Guitar (nylon)",
            "Acoustic Guitar (steel)",
            "Electric Guitar (jazz)",
            "Electric Guitar (clean)",
            "Electric Guitar (muted)",
            "Overdriven Guitar",
            "Distortion Guitar",
            "Guitar harmonics",
            "Acoustic Bass",
            "Electric Bass (finger)",
            "Electric Bass (pick)",
            "Fretless Bass",
            "Slap Bass 1",
            "Slap Bass 2",
            "Synth Bass 1",
            "Synth Bass 2",
            "Violin",
            "Viola",
            "Cello",
            "Contrabass",
            "Tremolo Strings",
            "Pizzicato Strings",
            "Orchestral Harp",
            "Timpani",
            "String Ensemble 1",
            "String Ensemble 2",
            "SynthStrings 1",
            "SynthStrings 2",
            "Choir Aahs",
            "Voice Oohs",
            "Synth Voice",
            "Orchestra Hit",
            "Trumpet",
            "Trombone",
            "Tuba",
            "Muted Trumpet",
            "French Horn",
            "Brass Section",
            "SynthBrass 1",
            "SynthBrass 2",
            "Soprano Sax",
            "Alto Sax",
            "Tenor Sax",
            "Baritone Sax",
            "Oboe",
            "English Horn",
            "Bassoon",
            "Clarinet",
            "Piccolo",
            "Flute",
            "Recorder",
            "Pan Flute",
            "Blown Bottle",
            "Shakuhachi",
            "Whistle",
            "Ocarina",
            "Lead 1 (square)",
            "Lead 2 (sawtooth)",
            "Lead 3 (calliope)",
            "Lead 4 (chiff)",
            "Lead 5 (charang)",
            "Lead 6 (voice)",
            "Lead 7 (fifths)",
            "Lead 8 (bass + lead)",
            "Pad 1 (new age)",
            "Pad 2 (warm)",
            "Pad 3 (polysynth)",
            "Pad 4 (choir)",
            "Pad 5 (bowed)",
            "Pad 6 (metallic)",
            "Pad 7 (halo)",
            "Pad 8 (sweep)",
            "FX 1 (rain)",
            "FX 2 (soundtrack)",
            "FX 3 (crystal)",
            "FX 4 (atmosphere)",
            "FX 5 (brightness)",
            "FX 6 (goblins)",
            "FX 7 (echoes)",
            "FX 8 (sci-fi)",
            "Sitar",
            "Banjo",
            "Shamisen",
            "Koto",
            "Kalimba",
            "Bag pipe",
            "Fiddle",
            "Shanai",
            "Tinkle Bell",
            "Agogo",
            "Steel Drums",
            "Woodblock",
            "Taiko Drum",
            "Melodic Tom",
            "Synth Drum",
            "Reverse Cymbal",
            "Guitar Fret Noise",
            "Breath Noise",
            "Seashore",
            "Bird Tweet",
            "Telephone Ring",
            "Helicopter",
            "Applause",
            "Gunshot"
        };
    }
}
