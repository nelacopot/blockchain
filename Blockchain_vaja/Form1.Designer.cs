namespace Blockchain_vaja
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            nodeName = new TextBox();
            ConnectButton = new Button();
            mineButton = new Button();
            Port = new TextBox();
            portButton = new Button();
            StatusLabel = new Label();
            blockchainLedger = new TextBox();
            Status = new Label();
            miningBox = new TextBox();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(22, 17);
            label1.Name = "label1";
            label1.Size = new Size(72, 15);
            label1.TabIndex = 0;
            label1.Text = "Node name:";
            // 
            // nodeName
            // 
            nodeName.Location = new Point(100, 14);
            nodeName.Name = "nodeName";
            nodeName.Size = new Size(100, 23);
            nodeName.TabIndex = 1;
            nodeName.TextChanged += nodeName_TextChanged;
            // 
            // ConnectButton
            // 
            ConnectButton.Location = new Point(206, 14);
            ConnectButton.Name = "ConnectButton";
            ConnectButton.Size = new Size(75, 23);
            ConnectButton.TabIndex = 2;
            ConnectButton.Text = "Connect";
            ConnectButton.UseVisualStyleBackColor = true;
            ConnectButton.Click += ConnectButton_Click;
            // 
            // mineButton
            // 
            mineButton.Location = new Point(287, 14);
            mineButton.Name = "mineButton";
            mineButton.Size = new Size(75, 23);
            mineButton.TabIndex = 3;
            mineButton.Text = "Mine";
            mineButton.UseVisualStyleBackColor = true;
            mineButton.Click += mineButton_Click;
            // 
            // Port
            // 
            Port.Location = new Point(436, 14);
            Port.Name = "Port";
            Port.Size = new Size(100, 23);
            Port.TabIndex = 4;
            Port.TextChanged += Port_TextChanged;
            // 
            // portButton
            // 
            portButton.Location = new Point(540, 15);
            portButton.Name = "portButton";
            portButton.Size = new Size(86, 23);
            portButton.TabIndex = 5;
            portButton.Text = "Connect port";
            portButton.UseVisualStyleBackColor = true;
            portButton.Click += portButton_Click;
            // 
            // StatusLabel
            // 
            StatusLabel.AutoSize = true;
            StatusLabel.Location = new Point(27, 62);
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Size = new Size(42, 15);
            StatusLabel.TabIndex = 6;
            StatusLabel.Text = "Status:";
            // 
            // blockchainLedger
            // 
            blockchainLedger.Location = new Point(28, 98);
            blockchainLedger.Multiline = true;
            blockchainLedger.Name = "blockchainLedger";
            blockchainLedger.ScrollBars = ScrollBars.Vertical;
            blockchainLedger.Size = new Size(334, 328);
            blockchainLedger.TabIndex = 8;
            // 
            // Status
            // 
            Status.AutoSize = true;
            Status.Location = new Point(100, 62);
            Status.Name = "Status";
            Status.Size = new Size(43, 15);
            Status.TabIndex = 10;
            Status.Text = "Offline";
            // 
            // miningBox
            // 
            miningBox.Location = new Point(384, 98);
            miningBox.Multiline = true;
            miningBox.Name = "miningBox";
            miningBox.ScrollBars = ScrollBars.Vertical;
            miningBox.Size = new Size(334, 328);
            miningBox.TabIndex = 11;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(miningBox);
            Controls.Add(Status);
            Controls.Add(blockchainLedger);
            Controls.Add(StatusLabel);
            Controls.Add(portButton);
            Controls.Add(Port);
            Controls.Add(mineButton);
            Controls.Add(ConnectButton);
            Controls.Add(nodeName);
            Controls.Add(label1);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox nodeName;
        private Button ConnectButton;
        private Button mineButton;
        private TextBox Port;
        private Button portButton;
        private Label StatusLabel;
        private TextBox blockchainLedger;
        private Label Status;
        private TextBox miningBox;
    }
}
