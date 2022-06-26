using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BroadcastClientGUI
{
    public partial class AddrPromptForm : Form
    {
        public event Action<Broadcast.Client.Client> OnClientInstantiated;

        public AddrPromptForm()
        {
            InitializeComponent();
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            IPHostEntry entry = null;
            if (IPAddress.TryParse(addressInputBox.Text.Trim(), out _)) {
                Broadcast.Client.Client client = new Broadcast.Client.Client(addressInputBox.Text.Trim(), Program.GAME_NAME, allowOnlyInterNetworkAddress: true);
                OnClientInstantiated?.Invoke(client);
            }
            else if ((entry = Dns.GetHostEntry(addressInputBox.Text.Trim())) != null) {
                Broadcast.Client.Client client = new Broadcast.Client.Client(addressInputBox.Text.Trim(), Program.GAME_NAME, allowOnlyInterNetworkAddress: true);
                OnClientInstantiated?.Invoke(client);
            }
            else {
                MessageBox.Show(this, $"Address {addressInputBox.Text} is not valid.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
