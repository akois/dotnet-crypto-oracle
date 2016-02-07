using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reactive;

//using CryptoOracle;

namespace OracleUI
{
    public partial class OracleMainForm : Form
    {
        
        private char [] _messageBytes =new char[48];
        private IDisposable _subscription=null;

        public OracleMainForm()
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 32;
            for (int i = 0; i < 48; i++) _messageBytes[i] = ' ';
            InitializeComponent();

            this.btnCancel.Enabled = false;
            this.btnDecrypt.Enabled = true;
        }

        private void OracleMainForm_Load(object sender, EventArgs e)
        {
            this.textCypher.Text ="f20bdba6ff29eed7b046d1df9fb7000058b1ffb4210a580f748b4ac714c001bd4a61044426fb515dad3f21f18aa577c0bdf302936266926ff37dbf7035d5eeb4";
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (null !=_subscription) _subscription.Dispose();

            _subscription = null;
            this.btnCancel.Enabled = false;
            this.btnDecrypt.Enabled = true;
            this.lblStatus.Text = "Canceled.";
        }

        private void btnDecrypt_Click(object sender, EventArgs e)
        {
            var observable = CryptoOracle.DecryptMessage(this.textCypher.Text);
            
            var observer=Observer.Create<Tuple<int,char>> (
                new Action<Tuple<int,char>> (tpl =>
                {
                    _messageBytes[tpl.Item1] = tpl.Item2;
                    this.Invoke((MethodInvoker)(()=>lblMessage.Text = new string(_messageBytes))); // runs on UI thread
                }),
                new Action<Exception>(ex => this.Invoke((MethodInvoker)(() => lblStatus.Text = "Error "+ex.Message))),
                new Action(() => this.Invoke((MethodInvoker)(() => lblStatus.Text = "Completed.")))
            );
            _subscription = observable.Subscribe(observer);
            this.btnCancel.Enabled = true;
            this.btnDecrypt.Enabled = false;
            this.lblStatus.Text = "Working...";
        }
    }
}
