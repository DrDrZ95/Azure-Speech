using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace speechtotextwpf
{
    public class CortanaVoice
    {
        public void StartVoice(string msg)
        {
            try
            {
                msg = !string.IsNullOrEmpty(msg) ? msg : "我没听到您说的话";
                var thread = new Thread(() =>
                {
                    using (var speechSyn = new SpeechSynthesizer())
                    {
                        speechSyn.Speak(msg);
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}