using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows.Forms;

using Broadcast.Shared;
using Broadcast.Client;

namespace BroadcastClientGUI
{
    public partial class MainForm : Form
    {
        AddrPromptForm prompt;

        Client client;
        bool isAvailable = false;
        Lobby lobby;

        public MainForm()
        {
            InitializeComponent();

            prompt = new AddrPromptForm();
            prompt.OnClientInstantiated += Prompt_OnClientInstantiated;

            prompt.ShowDialog(this);
        }

        private void Prompt_OnClientInstantiated(Client client)
        {
            prompt.OnClientInstantiated -= Prompt_OnClientInstantiated;

            this.client = client;
            prompt.Close();

            // Real init
            isAvailable = true;
            gameNameText.Text = Program.GAME_NAME;

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            createLobbyButton.Enabled = isAvailable && lobby == null;
            killLobbyButton.Enabled = isAvailable && lobby != null;
            gameNameText.Enabled = isAvailable;

            if (lobby == null) {
                lobbyInfoLabel.Text = string.Empty;
            }
            else {
                lobbyInfoLabel.Text = $"{lobby.game} on {lobby.map}\n{lobby.players}/{lobby.maxPlayers}";
            }

            refreshButton.Enabled = isAvailable;
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            if (!isAvailable) {
                return;
            }

            ChangeAvailability(false);

            Task.Run(async () =>
            {
                var lobbies = await client.FetchLobbies();

                Action a = () =>
                {
                    UpdateDataGridFromList(lobbies);
                    ChangeAvailability(true);
                };

                if (InvokeRequired) {
                    Invoke(a);
                }
                else {
                    a();
                }
            });
        }

        private void ChangeAvailability(bool availability)
        {
            isAvailable = availability;
            RefreshButtons();
        }

        private void UpdateDataGridFromList(List<Lobby> lobbies)
        {
            lobbyListDataGrid.SuspendLayout();
            lobbyListDataGrid.ReadOnly = false;

            lobbyListDataGrid.ClearSelection();
            lobbyListDataGrid.Rows.Clear();
            lobbyListDataGrid.Columns.Clear();

            if (lobbies != null) {

                var type = typeof(Lobby);
                var members = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.ExactBinding);

                lobbyListDataGrid.ColumnCount = members.Length;

                for (int i = 0; i < members.Length; i++) {
                    lobbyListDataGrid.Columns[i].Name = members[i].Name;
                }

                for (int i = 0; i < lobbies.Count; i++) {
                    var lobby = lobbies[i];

                    object[] cells = new object[members.Length];

                    for (int j = 0; j < members.Length; j++) {
                        var member = members[j];
                        cells[j] = member.GetValue(lobby);
                    }

                    lobbyListDataGrid.Rows.Add(cells);
                }
            }

            lobbyListDataGrid.ReadOnly = true;
            lobbyListDataGrid.ResumeLayout();
        }

        private void createLobbyButton_Click(object sender, EventArgs e)
        {
            if (!isAvailable) {
                return;
            }

            ChangeAvailability(false);

            try {
                byte[] addr = new byte[4];
                lobby = new Lobby();
                lobby.address = addr;
                lobby.title = "My Lobby";
                lobby.maxPlayers = 16;
                lobby.isPrivate = false;
                lobby.gameVersion = "MyGameName";
                lobby.game = gameNameText.Text;
                lobby.port = 1234;
                lobby.mods = new string[0];

                lobby.id = client.CreateLobby(lobby);
                if (lobby.id == 0) {
                    lobby = null;
                    MessageBox.Show(this, $"Could not create lobby", "WARNING", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex) {
                MessageBox.Show(this, ex.ToString(), "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            ChangeAvailability(true);
        }

        private void killLobbyButton_Click(object sender, EventArgs e)
        {
            if (!isAvailable) {
                return;
            }

            if (lobby == null) {
                return;
            }

            ChangeAvailability(false);

            Task.Run(async () =>
            {
                await client.DestroyLobby(lobby.id);
                lobby = null;

                Action a = () =>
                {
                    ChangeAvailability(true);
                };

                if (InvokeRequired) {
                    Invoke(a);
                }
                else {
                    a();
                }
            });
        }
    }
}
