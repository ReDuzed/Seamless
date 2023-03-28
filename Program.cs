using NAudio;
using NAudio.Codecs;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Compression;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace Seamless
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string? arg = "";
            if (args.Length == 1)
            {
                arg = args[0];
                goto EXISTS;
            }
            INPUT:
            Write("Input path to load audio files from: ", ConsoleColor.Yellow);
            {
                arg = Console.ReadLine();
            }
            EXISTS:
            Console.Clear();
            if (!Directory.Exists(arg))
            {
                Console.WriteLine($"Path {arg} not found.");
                goto INPUT;
            }
            string[] file = Directory.GetFiles(arg);
            int startIndex = -1;
            INDEX:
            Console.Clear();
            Write("Input start index of play list: ", ConsoleColor.Yellow);
            if (!int.TryParse(Console.ReadLine(), out startIndex) || startIndex < 1 || startIndex > file.Length)
            {
                goto INDEX;
            }
            startIndex--;
            Console.Clear();

            var wf = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).AudioClient.MixFormat;
            var ieee = WaveFormat.CreateIeeeFloatWaveFormat(wf.SampleRate, 2);
            var mixer = new MixingSampleProvider(ieee);
            var bwp = new BufferedWaveProvider[file.Length];

            mixer.ReadFully = true;
            WasapiOut output = new WasapiOut(AudioClientShareMode.Shared, true, 0);
            output.Init(mixer);
            output.Play();

            ManualResetEvent reset = new ManualResetEvent(false);
            TimeSpan elapsed = TimeSpan.Zero;
            
            Mp3FileReader mp3 = null;
            WaveFileReader wfr = null;
            Wave16ToFloatProvider wave = null;
            MultiplexingWaveProvider msw = null;
            WaveFormatConversionProvider wfc = null;
            ConcatenatingSampleProvider csp = null;
            Pcm8BitToSampleProvider pcm8 = null;
            Pcm16BitToSampleProvider pcm16 = null;
            Pcm24BitToSampleProvider pcm24 = null;
            Pcm32BitToSampleProvider pcm32 = null;
            
            int count = 0;

            ISampleProvider[] audio = new ISampleProvider[file.Length];
            TimeSpan span = TimeSpan.Zero;

            for (int i = startIndex; i < file.Length; i++)
            {
                if (file[i].EndsWith(".mp3") || file[i].EndsWith(".wav"))
                    WriteLine($"Loading:         {SafeFileName(file[i], "")}", ConsoleColor.Blue);
                if (file[i].EndsWith(".mp3"))
                {
                    mp3 = new Mp3FileReader(file[i]);
                    IWaveProvider iwp = null;
                    try
                    {
                        iwp = new MultiplexingWaveProvider(new[] { mp3 }, 2);
                    }
                    catch
                    {
                        iwp = wfr;
                    }
                    switch (mp3.WaveFormat.BitsPerSample)
                    {
                        case 8:
                            (wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, 2), iwp)).Reposition();
                            pcm8 = new Pcm8BitToSampleProvider(wfc);
                            audio[i] = pcm8;
                            goto default;
                        case 16:
                            (wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, 2), iwp)).Reposition();
                            pcm16 = new Pcm16BitToSampleProvider(wfc);
                            audio[i] = pcm16;
                            goto default;
                        case 24:
                            pcm24 = new Pcm24BitToSampleProvider(iwp);
                            //(wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, 2), pcm24.ToWaveProvider())).Reposition();
                            audio[i] = pcm24;
                            goto default;
                        case 32:
                            pcm32 = new Pcm32BitToSampleProvider(iwp);
                            //(wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, 2), pcm32.ToWaveProvider())).Reposition();
                            audio[i] = pcm32;
                            goto default;
                        default:
                            span += mp3.TotalTime;
                            break;
                    }
                }
                else if (file[i].EndsWith(".wav"))
                {
                    wfr = new WaveFileReader(file[i]);
                    IWaveProvider iwp = null;
                    try
                    { 
                        iwp = new MultiplexingWaveProvider(new[] { wfr }, 2);
                    }
                    catch 
                    { 
                        iwp = wfr;
                    }
                    switch (wfr.WaveFormat.BitsPerSample)
                    {
                        case 8:
                            (wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, 2), iwp)).Reposition();
                            pcm8 = new Pcm8BitToSampleProvider(wfc);
                            audio[i] = pcm8;
                            goto default;
                        case 16:
                            (wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, 2), iwp)).Reposition();
                            pcm16 = new Pcm16BitToSampleProvider(wfc);
                            audio[i] = pcm16;
                            goto default;
                        case 24:
                            pcm24 = new Pcm24BitToSampleProvider(iwp);
                            //(wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, 2), pcm24.ToWaveProvider())).Reposition();
                            audio[i] = pcm24;;
                            goto default;
                        case 32:
                            pcm32 = new Pcm32BitToSampleProvider(iwp);
                            //(wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, 2), pcm32.ToWaveProvider())).Reposition();
                            audio[i] = pcm32;
                            goto default;
                        default:
                            span += wfr.TotalTime;
                            break;
                    }
                }
            }
            var array = audio.Where(t => t != null);
            if (array.Count() > 0)
            { 
                csp = new ConcatenatingSampleProvider(array);
                mixer.AddMixerInput(csp);
            }
            else
            {                                //
                WriteLine($"\nStopped:         count = {array.Count()}", ConsoleColor.Cyan);
                reset.WaitOne(TimeSpan.FromSeconds(5));
                goto SKIP;
            }

            WriteLine($"\nFinal count:     {array.Count()}", ConsoleColor.Cyan);
            WriteLine($"Final length:    {span}", ConsoleColor.Cyan);
            WriteLine($"Playback begun.", ConsoleColor.Cyan);
            reset.WaitOne(span);

            Array.ForEach(audio, t => { t = null; });
            
            SKIP:
            Console.Clear();

            goto INPUT;
        }
        static void WriteLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
        static void Write(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }
        static string SafeFileName(string file, string extenstion)
        {
            string result = file;
            int index = 0;
            if (file.Contains("\\"))
            {
                result = file.Substring(file.LastIndexOf("\\") + 1);
            }
            else if (file.Contains("/"))
            {
                result = file.Substring(file.LastIndexOf("/") + 1);
            }
            if (file.ToLower().Contains(extenstion))
            {
                index = result.LastIndexOf(extenstion);
            }
            if (index == 0) index = result.Length;
            result = result.Substring(0, index);
            return result;
        }
    }
}