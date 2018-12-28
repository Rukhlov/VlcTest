using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VlcTest
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        public void StartRender(string appId)
        {

            videoControl1?.Start(appId);
        }

        public void StopRender()
        {
            videoControl1?.Stop();
        }
    }
}
