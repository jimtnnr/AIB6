
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AIB6
{
    public partial class VoiceRecorderControl : UserControl
    {
        private bool isRecording = false;
        private string accumulatedTranscript = "";

        private Process? recordingProcess = null;
        private DispatcherTimer? waveformTimer;
        private DispatcherTimer? micStatusTimer;
        private DateTime recordingStartTime;
        private const int MaxTranscriptLength = 4000;
        private const int MaxWaveformBars = 30;
        private Random waveformRandom = new Random();
        private bool isSoundDetected = true;
        private DateTime lastSoundTime = DateTime.Now;
        private DispatcherTimer? transcriptionDotsTimer;
        private int dotCount = 0;
        private DispatcherTimer? transcribingDotsTimer;
        private DispatcherTimer dotTimer;
        //public string FinalTranscript => TranscribedTextBox.Text;
        public event EventHandler<string>? TranscriptReady;
        public event EventHandler? ClearPressed;

      //  private void FinalizeTranscript()
       // {
          //  var finalText = TranscribedTextBox.Text;
         //   TranscriptReady?.Invoke(this, finalText);
       // }

        private string[] waveformFrames = new[]
        {
            "\n\n              ▁▂▃▄▅▆▇█▇▆▅▄▃▂▁              ",
            "\n\n              ▃▅▇▇▅▃▂▂▃▅▆▇▇              ",
            "\n\n              ▂▄▆▆▄▂▁▁▂▄▆▇█              ",
            "\n\n              ▅▇▇▆▅▄▃▂▁▂▃▄▅              "
        };
        private int waveformIndex = 0;

        public VoiceRecorderControl()
        {
            InitializeComponent();
            const int MaxWaveformBars = 93;

            for (int i = 0; i < MaxWaveformBars; i++)
            {
                WaveformPanel.Children.Add(new Border
                {
                    Width = 4,
                    Height = 1,
                    Background = Brushes.Transparent,
                    Margin = new Thickness(1, 0, 1, 0),
                    VerticalAlignment = VerticalAlignment.Bottom
                });
            }
            dotTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            dotTimer.Tick += (_, _) =>
            {
                dotCount = (dotCount + 1) % 4;
                MicStatusText.Text = "Transcribing" + new string('.', dotCount);
            };

        }

        private void ClearButton_Click(object? sender, RoutedEventArgs e)
        {
            accumulatedTranscript = "";
           // TranscribedTextBox.Text = "";
            MicStatusText.Text = "Text cleared. Ready to record.";
            //CharCountLabel.Text = "Characters: 0 / 4000";
            WaveformPanel.Children.Clear();
            ClearPressed?.Invoke(this, EventArgs.Empty);


        }

        private async void StartButton_Click(object? sender, RoutedEventArgs e)
        {
            string outputDir = "/tmp/airlock_voice";
            string wavPath = Path.Combine(outputDir, "temp.wav");
            string txtPath = Path.Combine(outputDir, "output.txt");
            Directory.CreateDirectory(outputDir);

            if (!isRecording)
            {
                isRecording = true;
                StartButton.Content = new Avalonia.Controls.Shapes.Path
                {
                    Width = 16,
                    Height = 16,
                    Stretch = Avalonia.Media.Stretch.Uniform,
                    Fill = Avalonia.Media.Brushes.Red,
                    Data = Geometry.Parse("M12,6a6,6 0 1,0 12,0a6,6 0 1,0 -12,0")
                };

                MicStatusText.Text = "Press to stop recording";
                recordingStartTime = DateTime.Now;

                micStatusTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                micStatusTimer.Tick += (_, _) =>
                {
                    var elapsed = DateTime.Now - recordingStartTime;
                    MicStatusText.Text = $"Recording... ({(int)elapsed.TotalSeconds}s)";
                };
                micStatusTimer.Start();

                waveformTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                double angle = 0;

                waveformTimer.Tick += (_, _) =>
                {
                    WaveformPanel.Children.Clear();

                    for (int i = 0; i < 100; i++)
                    {
                        double height = 6 + 4 * Math.Sin((angle + i) * 0.2);
                        var bar = new Border
                        {
                            Width = 5,
                            Height = height,
                            Background = Brushes.MediumPurple,
                            Margin = new Thickness(1),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
                        };

                        WaveformPanel.Children.Add(bar);
                    }

                    angle += 1;
                };




                waveformTimer.Start();

                recordingProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = $"-y -f alsa -i default {wavPath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                recordingProcess.Start();
            }
            else
            {
                if (recordingProcess != null && !recordingProcess.HasExited)
                {
                    waveformTimer?.Stop();
                    waveformTimer = null;
                    WaveformPanel.Children.Clear();

                    micStatusTimer?.Stop();
                    micStatusTimer = null;

                    recordingProcess.Kill();
                    recordingProcess.WaitForExit();
                    await Task.Delay(500);
                    
                }


                isRecording = false;
                StartButton.Content = new Avalonia.Controls.Shapes.Path
                {
                    Width = 16,
                    Height = 16,
                    Stretch = Avalonia.Media.Stretch.Uniform,
                    Fill = Avalonia.Media.Brushes.ForestGreen,
                    Data = Geometry.Parse("M4,2 L14,8 L4,14 Z") // classic right triangle
                };
                MicStatusText.Text = "Press to start recording";
                micStatusTimer?.Stop();
                micStatusTimer = null;
                WaveformPanel.Children.Clear();


                waveformTimer?.Stop();
                waveformTimer = null;
                waveformIndex = 0;
                WaveformPanel.Children.Clear();
                //string previousText = TranscribedTextBox.Text;
                dotCount = 0;
                transcribingDotsTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                transcribingDotsTimer.Tick += (_, _) =>
                {
                    dotCount = (dotCount + 1) % 4;
                    MicStatusText.Text = "Transcribing" + new string('.', dotCount);
                    StartButton.IsEnabled = false;
                };
                transcribingDotsTimer.Start();

                try
                {
                    var whisperProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "/home/jimtnnr/whisper.cpp/build/bin/whisper-cli",
                            Arguments = $"-m /home/jimtnnr/whisper.cpp/models/ggml-base.en.bin -f {wavPath} -otxt -of {outputDir}/output",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    whisperProcess.Start();
                    await whisperProcess.WaitForExitAsync();

                    if (File.Exists(txtPath))
                    {
                        string transcript = await File.ReadAllTextAsync(txtPath);

                        if (!string.IsNullOrWhiteSpace(transcript))
                        {
                            if (!string.IsNullOrWhiteSpace(accumulatedTranscript))
                                accumulatedTranscript += "\n";

                            accumulatedTranscript += transcript.Trim();

                            // Trim if too long
                            if (accumulatedTranscript.Length > MaxTranscriptLength)
                            {
                                accumulatedTranscript = accumulatedTranscript.Substring(
                                    accumulatedTranscript.Length - MaxTranscriptLength);
                            }
                        }

                        //TranscribedTextBox.Text = accumulatedTranscript;
                        transcribingDotsTimer?.Stop();
                        transcribingDotsTimer = null;
                        MicStatusText.Text = "Transcription complete";
                        StartButton.IsEnabled = true;
                        TranscriptReady?.Invoke(this, accumulatedTranscript);

                        // Update character counter
                        int currentLength = accumulatedTranscript.Length;
                        //CharCountLabel.Text = $"Characters: {currentLength} / {MaxTranscriptLength}";
                    }
                    else
                    {
                        //TranscribedTextBox.Text = "(No transcript found)";
                    }
                }
                catch (Exception ex)
                {
                    //TranscribedTextBox.Text = $"Error: {ex.Message}";
                }
            }
        }
    }
}
