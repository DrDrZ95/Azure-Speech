using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace speechtotextwpf
{
    public class BaiduVoice
    {

        public void StartVoice(string msg)
        {
            var thread = new Thread(() =>
            {
                string strFullFileName = "baidu.mp3";

                var APP_ID = "11718615";
                var API_KEY = "Nmp8VnnFcS1F2NhI3pxLyGGi";
                var SECRET_KEY = "0AKxk3FoevGp8GbIQ2v2r5GnuiIKxCWV";

                try
                {
                    var client = new Baidu.Aip.Speech.Tts(API_KEY, SECRET_KEY);

                    var result = client.Synthesis(msg);
                    if (result.Success)
                    {
                        var VoiceData = result.Data;
                        

                        using (Stream stream = new MemoryStream(VoiceData))
                        {
                            using (FileStream fs = new FileStream(strFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                            {
                                stream.CopyTo(fs);
                                stream.Close();
                            }

                            PlayWait(strFullFileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
            });

            //线程必须为单线程
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private void PlayWait(string file)
        {
            mciSendString(string.Format("open \"{0}\" alias media", file), null, 0, 0);

            mciSendString("play media wait", null, 0, 0);

            mciSendString("close media", null, 0, 0);
        }

        private const int NULL = 0, ERROR_SUCCESS = NULL;
        [DllImport("WinMm.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int mciSendString(string lpstrCommand, string lpstrReturnString, int uReturnLength, int hwndCallback);
    }
}