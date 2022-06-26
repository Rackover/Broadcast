namespace BroadcastClientGUI
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.GroupBox groupBox1;
            System.Windows.Forms.GroupBox groupBox2;
            System.Windows.Forms.Label label1;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.createLobbyButton = new System.Windows.Forms.Button();
            this.killLobbyButton = new System.Windows.Forms.Button();
            this.lobbyInfoLabel = new System.Windows.Forms.Label();
            this.refreshButton = new System.Windows.Forms.Button();
            this.lobbyListDataGrid = new System.Windows.Forms.DataGridView();
            this.gameNameText = new System.Windows.Forms.TextBox();
            groupBox1 = new System.Windows.Forms.GroupBox();
            groupBox2 = new System.Windows.Forms.GroupBox();
            label1 = new System.Windows.Forms.Label();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.lobbyListDataGrid)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            groupBox1.Controls.Add(this.createLobbyButton);
            groupBox1.Controls.Add(this.killLobbyButton);
            groupBox1.Location = new System.Drawing.Point(121, 230);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(95, 80);
            groupBox1.TabIndex = 7;
            groupBox1.TabStop = false;
            groupBox1.Text = "Lobby control";
            // 
            // createLobbyButton
            // 
            this.createLobbyButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.createLobbyButton.Location = new System.Drawing.Point(6, 19);
            this.createLobbyButton.Name = "createLobbyButton";
            this.createLobbyButton.Size = new System.Drawing.Size(83, 23);
            this.createLobbyButton.TabIndex = 1;
            this.createLobbyButton.Text = "Create";
            this.createLobbyButton.UseVisualStyleBackColor = true;
            this.createLobbyButton.Click += new System.EventHandler(this.createLobbyButton_Click);
            // 
            // killLobbyButton
            // 
            this.killLobbyButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.killLobbyButton.Location = new System.Drawing.Point(6, 51);
            this.killLobbyButton.Name = "killLobbyButton";
            this.killLobbyButton.Size = new System.Drawing.Size(83, 23);
            this.killLobbyButton.TabIndex = 6;
            this.killLobbyButton.Text = "Kill";
            this.killLobbyButton.UseVisualStyleBackColor = true;
            this.killLobbyButton.Click += new System.EventHandler(this.killLobbyButton_Click);
            // 
            // groupBox2
            // 
            groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            groupBox2.Controls.Add(this.lobbyInfoLabel);
            groupBox2.Location = new System.Drawing.Point(222, 230);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new System.Drawing.Size(208, 80);
            groupBox2.TabIndex = 8;
            groupBox2.TabStop = false;
            groupBox2.Text = "Lobby info";
            // 
            // lobbyInfoLabel
            // 
            this.lobbyInfoLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lobbyInfoLabel.Location = new System.Drawing.Point(6, 19);
            this.lobbyInfoLabel.Name = "lobbyInfoLabel";
            this.lobbyInfoLabel.Size = new System.Drawing.Size(196, 55);
            this.lobbyInfoLabel.TabIndex = 0;
            // 
            // refreshButton
            // 
            this.refreshButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.refreshButton.Location = new System.Drawing.Point(436, 230);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(146, 80);
            this.refreshButton.TabIndex = 2;
            this.refreshButton.Text = "Refresh lobbies";
            this.refreshButton.UseVisualStyleBackColor = true;
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);
            // 
            // lobbyListDataGrid
            // 
            this.lobbyListDataGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lobbyListDataGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.lobbyListDataGrid.Location = new System.Drawing.Point(12, 12);
            this.lobbyListDataGrid.Name = "lobbyListDataGrid";
            this.lobbyListDataGrid.Size = new System.Drawing.Size(570, 212);
            this.lobbyListDataGrid.TabIndex = 3;
            // 
            // gameNameText
            // 
            this.gameNameText.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.gameNameText.Location = new System.Drawing.Point(12, 246);
            this.gameNameText.Name = "gameNameText";
            this.gameNameText.Size = new System.Drawing.Size(103, 20);
            this.gameNameText.TabIndex = 9;
            // 
            // label1
            // 
            label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(12, 230);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(67, 13);
            label1.TabIndex = 10;
            label1.Text = "Game name:";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(594, 322);
            this.Controls.Add(label1);
            this.Controls.Add(this.gameNameText);
            this.Controls.Add(groupBox2);
            this.Controls.Add(groupBox1);
            this.Controls.Add(this.lobbyListDataGrid);
            this.Controls.Add(this.refreshButton);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "Broadcast Client GUI";
            groupBox1.ResumeLayout(false);
            groupBox2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.lobbyListDataGrid)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button createLobbyButton;
        private System.Windows.Forms.Button refreshButton;
        private System.Windows.Forms.DataGridView lobbyListDataGrid;
        private System.Windows.Forms.Button killLobbyButton;
        private System.Windows.Forms.Label lobbyInfoLabel;
        private System.Windows.Forms.TextBox gameNameText;
    }
}

