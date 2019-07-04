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
        #region ����ʶ��-��������/ģ��

        /// <summary>
        /// �Ƿ�ʹ����˷�
        /// </summary>
        public bool UseMicrophone { get; set; }

        /// <summary>
        /// �Ƿ�ʹ�÷���wav�ļ�
        /// </summary>
        public bool UseFileInput { get; set; }

        /// <summary>
        /// ��������ģ��
        /// </summary>
        public bool UseBaseModel { get; set; }

        /// <summary>
        /// ������Կ
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
        /// ����
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// ʶ������
        /// </summary>
        public string RecognitionLanguage { get; set; }
        
        // Ĭ������
        private const string defaultLocale = "zh-CN";
        // ��Կ
        private string subscriptionKey;
        private const string subscriptionKeyFileName = "SubscriptionKey.txt";
        // wav�ļ�·��
        private string wavFileName;

        // ֹͣ����ʶ������ Task
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
        /// ��ʼ��һ���µ������Ự��
        /// </summary>
        private void Initialize()
        {
            // Ĭ��ʹ����˷�
            this.UseMicrophone = true;
            this.stopButton.IsEnabled = false;
            this.micRadioButton.IsChecked = true;
            
            this.UseFileInput = false;
            this.UseBaseModel = true;

            //��ȡ�������Կ
            this.SubscriptionKey = this.GetValueFromIsolatedStorage(subscriptionKeyFileName);
        }

        /// <summary>
        /// ����StartButton�ĵ����¼�:
        /// 1.�������������Ӧ�İ�ť���Է�ֹ���ִ���
        /// 2.�����Կ�Ƿ���Ч
        /// 3.�����ʶ��wav�ļ����򲥷�
        /// 4.��ʼ������ʶ��
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
                    MessageBox.Show("��Կ�����ʧ!");
                    this.WriteLine(this.baseModelLogText, "--- Error : ��Կ�����ʧ! ---");
                }

                this.EnableButtons();
                return;
            }

            // ʶ��wav�ļ� ������������ȡ����
            if (!this.UseMicrophone)
            {
                wavFileName = GetFile();
                if (wavFileName.Length <= 0) return;
                Task.Run(() => this.PlayAudioFile());
            }
            

            // ���߳�ִ��
            if (this.UseBaseModel)
            {
                stopBaseRecognitionTaskCompletionSource = new TaskCompletionSource<int>();
                //���߳��첽
                Task.Run(async () => { await CreateBaseReco().ConfigureAwait(false); });
            }
        }


        /// <summary>
        /// ����StopButton�ĵ����¼�:
        /// ��ֹ��˷�����ʶ��
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
        /// ʹ�ã�����ģ�ͣ��ͣ����ԣ���ʼ��ʶ����:
        /// 1.������Կ�͵���������������Config
        /// 2.���ݲ�ͬ��ʽ����˷�¼�����͡�ʶ��wav�ļ��������в�ͬ����
        /// 3.�ȴ��첽����
        /// </summary>
        private async Task CreateBaseReco()
        {
            // ���ݵ�������Կ ��ʼ������
            var config = SpeechConfig.FromSubscription(this.SubscriptionKey, this.Region);
            config.SpeechRecognitionLanguage = this.RecognitionLanguage;

            //����ʶ����
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
        /// ��ʼʶ���¼�
        /// ����ʶ�𣬵ȴ��յ����ս����Ȼ��ֹͣʶ��
        /// </summary>
        /// <param name="recognizer">ʶ����</param>
        ///  <value>
        ///   <c>Base</c> if Baseline model; otherwise, 
        /// </value>
        private async Task RunRecognizer(SpeechRecognizer recognizer, TaskCompletionSource<int> source)
        {

            //�����¼�
            EventHandler<SpeechRecognitionEventArgs> recognizingHandler = (sender, e) => RecognizingEventHandler(e);

            //ʶ��������¼�
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

            //��ʼ,�ȴ�,ֹͣʶ�𣨵���ʶ��
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

        #region ����ʶ���¼��������

        /// <summary>
        /// ��;���
        /// </summary>
        private void RecognizingEventHandler(SpeechRecognitionEventArgs e)
        {
            var log = this.baseModelLogText;
            
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(e.Result.Text))
                {
                    this.SetCurrentText(this.baseModelCurrentText, "��û������˵��...");
                }
                else
                {
                    this.SetCurrentText(this.baseModelCurrentText, "���������ĶԻ���");
                }
            });
        }

        /// <summary>
        /// ��ȡ���ս��
        /// </summary>
        private void RecognizedEventHandler(SpeechRecognitionEventArgs e)
        {
            TextBox log;

            log = this.baseModelLogText;
            
            this.WriteLine(log);
            this.WriteLine(log, $" ��------- [ʶ�𵽵Ļ�] -------�� ");
            
            this.WriteLine(log, e.Result.Text);
            var result = msbot.TalkMessage(e.Result.Text).Result;
            this.SetCurrentText(this.baseModelCurrentText, " -- ��\n\n��" + e.Result.Text + "��\n\n -- ������\n\n��" + result + "��");
            

            // ���������ΪJson��ʽ
            // string json = e.Result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);

        }

        /// <summary>
        /// ��־ȡ���¼�
        /// ����TaskCompletionSource����Ϊ0���Ա㴥��ʶ��ֹͣ
        /// </summary>
        private void CanceledEventHandler(SpeechRecognitionCanceledEventArgs e, TaskCompletionSource<int> source)
        {
            var log = this.baseModelLogText;
            source.TrySetResult(0);
            this.WriteLine(log, "--- ʶ����� ---");
            this.WriteLine(log);
        }

        /// <summary>
        /// �Ự�����¼��������
        /// </summary>
        private void SessionStartedEventHandler(SessionEventArgs e)
        {
            var log = this.baseModelLogText;
            this.WriteLine(log, String.Format(CultureInfo.InvariantCulture, "--- ��ʼ¼�� ---"));
        }

        /// <summary>
        /// �Ựֹͣ�¼�������򡣲���TaskCompletionSource����Ϊ0���Ա㴥��ʶ��ֹͣ
        /// </summary>
        private void SessionStoppedEventHandler(SessionEventArgs e, TaskCompletionSource<int> source)
        {
            var log = this.baseModelLogText;
            this.WriteLine(log, String.Format(CultureInfo.InvariantCulture, "\n--- ����¼�� ---"));
            source.TrySetResult(0);
            
            //����ִ�к����
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

        #region ��������
        /// <summary>
        /// ��ȡ��Կ
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
        /// д����Կ
        /// </summary>
        /// <param name="fileName">�洢��Կ���ļ�����</param>
        /// <param name="key">��Կ</param>
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
        /// ��Կ���水ť
        /// </summary>
        private void SaveKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveKeyToIsolatedStorage(subscriptionKeyFileName, this.SubscriptionKey);
                MessageBox.Show("��Կ�����浽�����С�\n�´����Ͳ���Ҫ��ճ���ˡ�", "Keys");
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "����ʧ��. ����ԭ��: " + exception.Message,
                    "Keys",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// �����Կ
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
        /// ��鲢��ȡwav�ļ�
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
                MessageBox.Show("�ļ�������!");
                this.WriteLine(this.baseModelLogText, "--- Error : �ļ�������! ---");
                this.EnableButtons();
                return "";
            }
            return filePath;
        }

        /// <summary>
        /// ʶ�𲥷�wav
        /// </summary>
        private void PlayAudioFile()
        {
            SoundPlayer player = new SoundPlayer(wavFileName);
            player.Load();
            player.Play();
        }

        /// <summary>
        /// ��ʼʶ��
        /// </summary>
        private void LogRecognitionStart(TextBox log, TextBlock currentText)
        {
            string recoSource;
            recoSource = this.UseMicrophone ? "��˷�ʶ��" : "wav�ļ�ʶ��";

            this.SetCurrentText(currentText, string.Empty);
            log.Clear();
            this.WriteLine(log, "\n--- ��ʼ " + recoSource + " ��ǰʶ������Ϊ " + defaultLocale + " ----\n\n");
        }

        /// <summary>
        /// ����������ʾ
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
        /// INotifyPropertyChanged�ӿڵİ�������
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

        #region �¼�
        /// <summary>
        /// ʵ��INotifyPropertyChanged�ӿ�
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// �����System.Windows.Window���ر��¼���
        /// </summary>
        /// <param name="e">An System.EventArgs that contains the event data.</param>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }

        #endregion �¼�

    }
}
