namespace MicrosoftSpeech.WpfSpeechRecognitionSample
{
    using System;
    using System.Globalization;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Media;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using Forms = System.Windows.Forms;
    using System.IO.IsolatedStorage;

    using Microsoft.CognitiveServices.Speech;
    using Microsoft.CognitiveServices.Speech.Audio;
    using speechtotextwpf;

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region 语音识别-基本属性/模型

        /// <summary>
        /// 是否使用麦克风
        /// </summary>
        public bool UseMicrophone { get; set; }

        /// <summary>
        /// 是否使用分析wav文件
        /// </summary>
        public bool UseFileInput { get; set; }

        /// <summary>
        /// 语音基本模型
        /// </summary>
        public bool UseBaseModel { get; set; }

        /// <summary>
        /// 语音密钥
        /// </summary>
        public string SubscriptionKey
        {
            get
            {
                return this.subscriptionKey;
            }

            set
            {
                this.subscriptionKey = value?.Trim();
                this.OnPropertyChanged<string>();
            }
        }

        /// <summary>
        /// 地区
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// 识别语言
        /// </summary>
        public string RecognitionLanguage { get; set; }
        
        // 默认语言
        private const string defaultLocale = "zh-CN";
        // 密钥
        private string subscriptionKey;
        private const string subscriptionKeyFileName = "SubscriptionKey.txt";
        // wav文件路径
        private string wavFileName;

        // 停止基本识别任务 Task
        // https://blogs.msdn.microsoft.com/pfxteam/2011/10/02/keeping-async-methods-alive/
        private TaskCompletionSource<int> stopBaseRecognitionTaskCompletionSource;

        #endregion

        public MSBot msbot;

        public MainWindow()
        {
            this.InitializeComponent();
            this.Initialize();
            msbot = new MSBot();
        }


        /// <summary>
        /// 初始化一个新的语音会话。
        /// </summary>
        private void Initialize()
        {
            // 默认使用麦克风
            this.UseMicrophone = true;
            this.stopButton.IsEnabled = false;
            this.micRadioButton.IsChecked = true;
            
            this.UseFileInput = false;
            this.UseBaseModel = true;

            //获取保存的密钥
            this.SubscriptionKey = this.GetValueFromIsolatedStorage(subscriptionKeyFileName);
        }

        /// <summary>
        /// 处理StartButton的单击事件:
        /// 1.根据情况禁用相应的按钮，以防止出现错乱
        /// 2.检查密钥是否有效
        /// 3.如果是识别wav文件，则播放
        /// 4.初始化语音识别
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            this.startButton.IsEnabled = false;
            this.stopButton.IsEnabled = true;
            this.radioGroup.IsEnabled = false;
            this.optionPanel.IsEnabled = false;
            this.LogRecognitionStart(this.baseModelLogText, this.baseModelCurrentText);
            wavFileName = "";

            this.Region = ((ComboBoxItem)regionComboBox.SelectedItem).Tag.ToString();
            this.RecognitionLanguage = ((ComboBoxItem)languageComboBox.SelectedItem).Tag.ToString();

            if (!AreKeysValid())
            {
                if (this.UseBaseModel)
                {
                    MessageBox.Show("密钥错误或丢失!");
                    this.WriteLine(this.baseModelLogText, "--- Error : 密钥错误或丢失! ---");
                }

                this.EnableButtons();
                return;
            }

            // 识别wav文件 播放语音（可取消）
            if (!this.UseMicrophone)
            {
                wavFileName = GetFile();
                if (wavFileName.Length <= 0) return;
                Task.Run(() => this.PlayAudioFile());
            }
            

            // 多线程执行
            if (this.UseBaseModel)
            {
                stopBaseRecognitionTaskCompletionSource = new TaskCompletionSource<int>();
                //多线程异步
                Task.Run(async () => { await CreateBaseReco().ConfigureAwait(false); });
            }
        }


        /// <summary>
        /// 处理StopButton的单击事件:
        /// 中止麦克风语音识别
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            this.stopButton.IsEnabled = false;
            if (this.UseBaseModel)
            {
                stopBaseRecognitionTaskCompletionSource.TrySetResult(0);
            }

            EnableButtons();
        }

        /// <summary>
        /// 使用（语音模型）和（语言）初始化识别器:
        /// 1.利用密钥和地区创建语音配置Config
        /// 2.根据不同方式【麦克风录音】和【识别wav文件】，进行不同操作
        /// 3.等待异步运行
        /// </summary>
        private async Task CreateBaseReco()
        {
            // 根据地区和密钥 初始化配置
            var config = SpeechConfig.FromSubscription(this.SubscriptionKey, this.Region);
            config.SpeechRecognitionLanguage = this.RecognitionLanguage;

            //语音识别器
            SpeechRecognizer basicRecognizer;
            if (this.UseMicrophone)
            {
                using (basicRecognizer = new SpeechRecognizer(config))
                {
                    await this.RunRecognizer(basicRecognizer, stopBaseRecognitionTaskCompletionSource).ConfigureAwait(false);
                }
            }
            else
            {
                using (var audioInput = AudioConfig.FromWavFileInput(wavFileName))
                {
                    using (basicRecognizer = new SpeechRecognizer(config, audioInput))
                    {
                        await this.RunRecognizer(basicRecognizer, stopBaseRecognitionTaskCompletionSource).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// 开始识别事件
        /// 启动识别，等待收到最终结果，然后停止识别
        /// </summary>
        /// <param name="recognizer">识别器</param>
        ///  <value>
        ///   <c>Base</c> if Baseline model; otherwise, 
        /// </value>
        private async Task RunRecognizer(SpeechRecognizer recognizer, TaskCompletionSource<int> source)
        {

            //创建事件
            EventHandler<SpeechRecognitionEventArgs> recognizingHandler = (sender, e) => RecognizingEventHandler(e);

            //识别器添加事件
            recognizer.Recognizing += recognizingHandler;


            EventHandler<SpeechRecognitionEventArgs> recognizedHandler = (sender, e) => RecognizedEventHandler(e);

            EventHandler<SpeechRecognitionCanceledEventArgs> canceledHandler = (sender, e) => CanceledEventHandler(e, source);
            EventHandler<SessionEventArgs> sessionStartedHandler = (sender, e) => SessionStartedEventHandler(e);
            EventHandler<SessionEventArgs> sessionStoppedHandler = (sender, e) => SessionStoppedEventHandler(e, source);
            EventHandler<RecognitionEventArgs> speechStartDetectedHandler = (sender, e) => SpeechDetectedEventHandler(e, "start");
            EventHandler<RecognitionEventArgs> speechEndDetectedHandler = (sender, e) => SpeechDetectedEventHandler(e, "end");

            recognizer.Recognized += recognizedHandler;
            recognizer.Canceled += canceledHandler;
            recognizer.SessionStarted += sessionStartedHandler;
            recognizer.SessionStopped += sessionStoppedHandler;
            recognizer.SpeechStartDetected -= speechStartDetectedHandler;
            recognizer.SpeechEndDetected -= speechEndDetectedHandler;

            //开始,等待,停止识别（单次识别）
            //await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
            await recognizer.RecognizeOnceAsync();
            await source.Task.ConfigureAwait(false);
            //await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

            this.EnableButtons();


            recognizer.Recognizing -= recognizingHandler;
            recognizer.Recognized -= recognizedHandler;
            recognizer.Canceled -= canceledHandler;
            recognizer.SessionStarted -= sessionStartedHandler;
            recognizer.SessionStopped -= sessionStoppedHandler;
            recognizer.SpeechStartDetected -= speechStartDetectedHandler;
            recognizer.SpeechEndDetected -= speechEndDetectedHandler;
        }

        #region 语音识别（事件处理程序）

        /// <summary>
        /// 中途检测
        /// </summary>
        private void RecognizingEventHandler(SpeechRecognitionEventArgs e)
        {
            var log = this.baseModelLogText;
            
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(e.Result.Text))
                {
                    this.SetCurrentText(this.baseModelCurrentText, "我没听到您说话...");
                }
                else
                {
                    this.SetCurrentText(this.baseModelCurrentText, "我听到您的对话了");
                }
            });
        }

        /// <summary>
        /// 获取最终结果
        /// </summary>
        private void RecognizedEventHandler(SpeechRecognitionEventArgs e)
        {
            TextBox log;

            log = this.baseModelLogText;
            
            this.WriteLine(log);
            this.WriteLine(log, $" 【------- [识别到的话] -------】 ");
            
            this.WriteLine(log, e.Result.Text);
            var result = msbot.TalkMessage(e.Result.Text).Result;
            this.SetCurrentText(this.baseModelCurrentText, " -- 您\n\n“" + e.Result.Text + "”\n\n -- 机器人\n\n“" + result + "“");
            

            // 将结果返回为Json格式
            // string json = e.Result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);

        }

        /// <summary>
        /// 日志取消事件
        /// 并将TaskCompletionSource设置为0，以便触发识别停止
        /// </summary>
        private void CanceledEventHandler(SpeechRecognitionCanceledEventArgs e, TaskCompletionSource<int> source)
        {
            var log = this.baseModelLogText;
            source.TrySetResult(0);
            this.WriteLine(log, "--- 识别结束 ---");
            this.WriteLine(log);
        }

        /// <summary>
        /// 会话启动事件处理程序
        /// </summary>
        private void SessionStartedEventHandler(SessionEventArgs e)
        {
            var log = this.baseModelLogText;
            this.WriteLine(log, String.Format(CultureInfo.InvariantCulture, "--- 开始录音 ---"));
        }

        /// <summary>
        /// 会话停止事件处理程序。并将TaskCompletionSource设置为0，以便触发识别停止
        /// </summary>
        private void SessionStoppedEventHandler(SessionEventArgs e, TaskCompletionSource<int> source)
        {
            var log = this.baseModelLogText;
            this.WriteLine(log, String.Format(CultureInfo.InvariantCulture, "\n--- 结束录音 ---"));
            source.TrySetResult(0);
            
            //单次执行后结束
            Dispatcher.Invoke(() =>
            {
                this.stopButton.IsEnabled = false;
                if (this.UseBaseModel)
                {
                    stopBaseRecognitionTaskCompletionSource.TrySetResult(0);
                }

                EnableButtons();
            });
        }

        private void SpeechDetectedEventHandler(RecognitionEventArgs e, string eventType)
        {
            var log = this.baseModelLogText;
            this.WriteLine(log, String.Format(CultureInfo.InvariantCulture, "Speech recognition: Speech {0} detected event: {1}.",
                eventType, e.ToString()));
        }

        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取密钥
        /// </summary>
        private string GetValueFromIsolatedStorage(string fileName)
        {
            string value = null;
            using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null))
            {
                try
                {
                    using (var iStream = new IsolatedStorageFileStream(fileName, FileMode.Open, isoStore))
                    {
                        using (var reader = new StreamReader(iStream))
                        {
                            value = reader.ReadLine();
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    value = null;
                }
            }

            return value;
        }

        /// <summary>
        /// 写入密钥
        /// </summary>
        /// <param name="fileName">存储密钥的文件名称</param>
        /// <param name="key">密钥</param>
        private static void SaveKeyToIsolatedStorage(string fileName, string key)
        {
            if (fileName != null && key != null)
            {
                using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null))
                {
                    using (var oStream = new IsolatedStorageFileStream(fileName, FileMode.Create, isoStore))
                    {
                        using (var writer = new StreamWriter(oStream))
                        {
                            writer.WriteLine(key);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 密钥保存按钮
        /// </summary>
        private void SaveKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveKeyToIsolatedStorage(subscriptionKeyFileName, this.SubscriptionKey);
                MessageBox.Show("密钥被保存到缓存中。\n下次您就不需要再粘贴了。", "Keys");
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "保存失败. 具体原因: " + exception.Message,
                    "Keys",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 检查密钥
        /// </summary>
        private bool AreKeysValid()
        {
            if (this.subscriptionKey == null || this.subscriptionKey.Length <= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查并获取wav文件
        /// </summary>
        public string GetFile()
        {
            string filePath = "";
            this.Dispatcher.Invoke(() =>
            {
                filePath = this.fileNameTextBox.Text;
            });
            if (!File.Exists(filePath))
            {
                MessageBox.Show("文件不存在!");
                this.WriteLine(this.baseModelLogText, "--- Error : 文件不存在! ---");
                this.EnableButtons();
                return "";
            }
            return filePath;
        }

        /// <summary>
        /// 识别播放wav
        /// </summary>
        private void PlayAudioFile()
        {
            SoundPlayer player = new SoundPlayer(wavFileName);
            player.Load();
            player.Play();
        }

        /// <summary>
        /// 开始识别
        /// </summary>
        private void LogRecognitionStart(TextBox log, TextBlock currentText)
        {
            string recoSource;
            recoSource = this.UseMicrophone ? "麦克风识别" : "wav文件识别";

            this.SetCurrentText(currentText, string.Empty);
            log.Clear();
            this.WriteLine(log, "\n--- 开始 " + recoSource + " 当前识别语言为 " + defaultLocale + " ----\n\n");
        }

        /// <summary>
        /// 代码详情显示
        /// </summary>
        private void WriteLine(TextBox log)
        {
            this.WriteLine(log, string.Empty);
        }

        private void WriteLine(TextBox log, string format, params object[] args)
        {
            var formattedStr = string.Format(CultureInfo.InvariantCulture, format, args);
            Trace.WriteLine(formattedStr);
            this.Dispatcher.Invoke(() =>
            {
                log.AppendText((formattedStr + "\n"));
                log.ScrollToEnd();
            });
        }

        private void SetCurrentText(TextBlock textBlock, string text)
        {
            this.Dispatcher.Invoke(() =>
            {
                textBlock.Text = text;
            });
        }

        /// <summary>
        /// INotifyPropertyChanged接口的帮助函数
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="caller">Property name</param>
        private void OnPropertyChanged<T>([CallerMemberName]string caller = null)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(caller));
            }
        }

        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            this.EnableButtons();
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            Forms.FileDialog fileDialog = new Forms.OpenFileDialog();
            fileDialog.ShowDialog();
            this.fileNameTextBox.Text = fileDialog.FileName;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://azure.microsoft.com/services/cognitive-services/");
        }

        private void EnableButtons()
        {
            this.Dispatcher.Invoke(() =>
            {
                this.startButton.IsEnabled = true;
                this.radioGroup.IsEnabled = true;
                this.optionPanel.IsEnabled = true;
            });
        }
        #endregion

        #region 事件
        /// <summary>
        /// 实现INotifyPropertyChanged接口
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 提高了System.Windows.Window。关闭事件。
        /// </summary>
        /// <param name="e">An System.EventArgs that contains the event data.</param>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }

        #endregion 事件

    }
}
