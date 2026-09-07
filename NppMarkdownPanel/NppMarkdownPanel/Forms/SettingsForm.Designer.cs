namespace NppMarkdownPanel.Forms
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true when managed resources should be disposed; otherwise, false.</param>
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
        /// Required method for Designer support — do not modify
        /// the contents of this method with the code editor.
        ///
        /// Layout: settings grouped into functional GroupBoxes (rendering
        /// engine / preview appearance / HTML auto-export / file types /
        /// panel behavior). The form font is assigned in the ctor
        /// (SystemFonts.MessageBoxFont) so the dialog follows the system
        /// UI font instead of a hardcoded small one; coordinates below are
        /// designed for 9pt at 96 DPI.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            this.panel1 = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.grpRendering = new System.Windows.Forms.GroupBox();
            this.comboRenderingEngine = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.grpPreview = new System.Windows.Forms.GroupBox();
            this.trackBar1 = new System.Windows.Forms.TrackBar();
            this.lblZoomValue = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.tbCssFile = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnChooseCss = new System.Windows.Forms.Button();
            this.btnDefaultCss = new System.Windows.Forms.Button();
            this.tbDarkmodeCssFile = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnChooseDarkmodeCss = new System.Windows.Forms.Button();
            this.btnDefaultDarkmodeCss = new System.Windows.Forms.Button();
            this.grpExport = new System.Windows.Forms.GroupBox();
            this.tbHtmlFile = new System.Windows.Forms.TextBox();
            this.lblHtmlFile = new System.Windows.Forms.Label();
            this.btnChooseHtml = new System.Windows.Forms.Button();
            this.btnResetHtml = new System.Windows.Forms.Button();
            this.grpFileTypes = new System.Windows.Forms.GroupBox();
            this.tbFileExt = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.btnDefaultFileExt = new System.Windows.Forms.Button();
            this.cbAllowAllExtensions = new System.Windows.Forms.CheckBox();
            this.cbFilesWithNoExt = new System.Windows.Forms.CheckBox();
            this.grpPanel = new System.Windows.Forms.GroupBox();
            this.cbAutoShowPanel = new System.Windows.Forms.CheckBox();
            this.cbShowToolbar = new System.Windows.Forms.CheckBox();
            this.cbShowStatusbar = new System.Windows.Forms.CheckBox();
            this.cbEnableThreeStateToggle = new System.Windows.Forms.CheckBox();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.sblInvalidHtmlPath = new System.Windows.Forms.ToolStripStatusLabel();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.grpRendering.SuspendLayout();
            this.grpPreview.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar1)).BeginInit();
            this.grpExport.SuspendLayout();
            this.grpFileTypes.SuspendLayout();
            this.grpPanel.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            //
            // panel1 (header)
            //
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.pictureBox1);
            this.panel1.Location = new System.Drawing.Point(2, 1);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(697, 56);
            this.panel1.TabIndex = 0;
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(44, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(219, 25);
            this.label1.TabIndex = 1;
            this.label1.Text = "Markdown Panel Settings";
            //
            // pictureBox1
            //
            this.pictureBox1.Image = global::NppMarkdownPanel.Properties.Resources.markdown_16x16_solid;
            this.pictureBox1.Location = new System.Drawing.Point(12, 16);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(24, 20);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            //
            // grpRendering
            //
            this.grpRendering.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpRendering.Controls.Add(this.label6);
            this.grpRendering.Controls.Add(this.comboRenderingEngine);
            this.grpRendering.Location = new System.Drawing.Point(12, 62);
            this.grpRendering.Name = "grpRendering";
            this.grpRendering.Size = new System.Drawing.Size(687, 66);
            this.grpRendering.TabIndex = 1;
            this.grpRendering.TabStop = false;
            this.grpRendering.Text = "Rendering Engine";
            //
            // label6
            //
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(15, 29);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(160, 20);
            this.label6.TabIndex = 0;
            this.label6.Text = "HTML rendering engine:";
            //
            // comboRenderingEngine
            //
            this.comboRenderingEngine.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboRenderingEngine.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRenderingEngine.FormattingEnabled = true;
            this.comboRenderingEngine.Items.AddRange(new object[] {
            "Edge (WebView 2)",
            "Internet Explorer 11 (WebView 1)"});
            this.comboRenderingEngine.Location = new System.Drawing.Point(196, 25);
            this.comboRenderingEngine.Name = "comboRenderingEngine";
            this.comboRenderingEngine.Size = new System.Drawing.Size(474, 26);
            this.comboRenderingEngine.TabIndex = 1;
            this.comboRenderingEngine.SelectedIndexChanged += new System.EventHandler(this.comboRenderingEngine_SelectedIndexChanged);
            //
            // grpPreview
            //
            this.grpPreview.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpPreview.Controls.Add(this.label3);
            this.grpPreview.Controls.Add(this.trackBar1);
            this.grpPreview.Controls.Add(this.lblZoomValue);
            this.grpPreview.Controls.Add(this.label2);
            this.grpPreview.Controls.Add(this.tbCssFile);
            this.grpPreview.Controls.Add(this.btnChooseCss);
            this.grpPreview.Controls.Add(this.btnDefaultCss);
            this.grpPreview.Controls.Add(this.label4);
            this.grpPreview.Controls.Add(this.tbDarkmodeCssFile);
            this.grpPreview.Controls.Add(this.btnChooseDarkmodeCss);
            this.grpPreview.Controls.Add(this.btnDefaultDarkmodeCss);
            this.grpPreview.Location = new System.Drawing.Point(12, 134);
            this.grpPreview.Name = "grpPreview";
            this.grpPreview.Size = new System.Drawing.Size(687, 190);
            this.grpPreview.TabIndex = 2;
            this.grpPreview.TabStop = false;
            this.grpPreview.Text = "Preview Appearance";
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 33);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(101, 20);
            this.label3.TabIndex = 0;
            this.label3.Text = "Zoom level:";
            //
            // trackBar1
            //
            this.trackBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.trackBar1.LargeChange = 1;
            this.trackBar1.Location = new System.Drawing.Point(196, 26);
            this.trackBar1.Maximum = 200;
            this.trackBar1.Minimum = 50;
            this.trackBar1.Name = "trackBar1";
            this.trackBar1.Size = new System.Drawing.Size(400, 56);
            this.trackBar1.TabIndex = 1;
            this.trackBar1.TickFrequency = 5;
            this.trackBar1.TickStyle = System.Windows.Forms.TickStyle.Both;
            this.trackBar1.Value = 130;
            this.trackBar1.ValueChanged += new System.EventHandler(this.trackBar1_ValueChanged);
            //
            // lblZoomValue
            //
            this.lblZoomValue.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblZoomValue.AutoSize = true;
            this.lblZoomValue.Location = new System.Drawing.Point(612, 40);
            this.lblZoomValue.Name = "lblZoomValue";
            this.lblZoomValue.Size = new System.Drawing.Size(45, 20);
            this.lblZoomValue.TabIndex = 2;
            this.lblZoomValue.Text = "130%";
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 92);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(120, 20);
            this.label2.TabIndex = 3;
            this.label2.Text = "CSS file (light):";
            //
            // tbCssFile
            //
            this.tbCssFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbCssFile.Location = new System.Drawing.Point(196, 88);
            this.tbCssFile.Name = "tbCssFile";
            this.tbCssFile.Size = new System.Drawing.Size(330, 26);
            this.tbCssFile.TabIndex = 4;
            this.tbCssFile.TextChanged += new System.EventHandler(this.tbCssFile_TextChanged);
            //
            // btnChooseCss
            //
            this.btnChooseCss.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnChooseCss.Location = new System.Drawing.Point(532, 86);
            this.btnChooseCss.Name = "btnChooseCss";
            this.btnChooseCss.Size = new System.Drawing.Size(40, 30);
            this.btnChooseCss.TabIndex = 5;
            this.btnChooseCss.Text = "...";
            this.btnChooseCss.UseVisualStyleBackColor = true;
            this.btnChooseCss.Click += new System.EventHandler(this.btnChooseCss_Click);
            //
            // btnDefaultCss
            //
            this.btnDefaultCss.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDefaultCss.Location = new System.Drawing.Point(578, 86);
            this.btnDefaultCss.Name = "btnDefaultCss";
            this.btnDefaultCss.Size = new System.Drawing.Size(92, 30);
            this.btnDefaultCss.TabIndex = 6;
            this.btnDefaultCss.Text = "Default";
            this.btnDefaultCss.UseVisualStyleBackColor = true;
            this.btnDefaultCss.Click += new System.EventHandler(this.button1_Click);
            //
            // label4
            //
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(15, 144);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(114, 20);
            this.label4.TabIndex = 7;
            this.label4.Text = "CSS file (dark):";
            //
            // tbDarkmodeCssFile
            //
            this.tbDarkmodeCssFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbDarkmodeCssFile.Location = new System.Drawing.Point(196, 140);
            this.tbDarkmodeCssFile.Name = "tbDarkmodeCssFile";
            this.tbDarkmodeCssFile.Size = new System.Drawing.Size(330, 26);
            this.tbDarkmodeCssFile.TabIndex = 8;
            this.tbDarkmodeCssFile.TextChanged += new System.EventHandler(this.tbDarkmodeCssFile_TextChanged);
            //
            // btnChooseDarkmodeCss
            //
            this.btnChooseDarkmodeCss.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnChooseDarkmodeCss.Location = new System.Drawing.Point(532, 138);
            this.btnChooseDarkmodeCss.Name = "btnChooseDarkmodeCss";
            this.btnChooseDarkmodeCss.Size = new System.Drawing.Size(40, 30);
            this.btnChooseDarkmodeCss.TabIndex = 9;
            this.btnChooseDarkmodeCss.Text = "...";
            this.btnChooseDarkmodeCss.UseVisualStyleBackColor = true;
            this.btnChooseDarkmodeCss.Click += new System.EventHandler(this.btnChooseCss_Click);
            //
            // btnDefaultDarkmodeCss
            //
            this.btnDefaultDarkmodeCss.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDefaultDarkmodeCss.Location = new System.Drawing.Point(578, 138);
            this.btnDefaultDarkmodeCss.Name = "btnDefaultDarkmodeCss";
            this.btnDefaultDarkmodeCss.Size = new System.Drawing.Size(92, 30);
            this.btnDefaultDarkmodeCss.TabIndex = 10;
            this.btnDefaultDarkmodeCss.Text = "Default";
            this.btnDefaultDarkmodeCss.UseVisualStyleBackColor = true;
            this.btnDefaultDarkmodeCss.Click += new System.EventHandler(this.btnDefaultDarkmodeCss_Click);
            //
            // grpExport
            //
            this.grpExport.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpExport.Controls.Add(this.lblHtmlFile);
            this.grpExport.Controls.Add(this.tbHtmlFile);
            this.grpExport.Controls.Add(this.btnChooseHtml);
            this.grpExport.Controls.Add(this.btnResetHtml);
            this.grpExport.Location = new System.Drawing.Point(12, 330);
            this.grpExport.Name = "grpExport";
            this.grpExport.Size = new System.Drawing.Size(687, 96);
            this.grpExport.TabIndex = 3;
            this.grpExport.TabStop = false;
            this.grpExport.Text = "HTML Auto-Export";
            //
            // lblHtmlFile
            //
            this.lblHtmlFile.Location = new System.Drawing.Point(15, 30);
            this.lblHtmlFile.Name = "lblHtmlFile";
            this.lblHtmlFile.Size = new System.Drawing.Size(172, 52);
            this.lblHtmlFile.TabIndex = 0;
            this.lblHtmlFile.Text = "Auto-save the current preview HTML to this file (rewritten on every render):";
            //
            // tbHtmlFile
            //
            this.tbHtmlFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbHtmlFile.Location = new System.Drawing.Point(196, 38);
            this.tbHtmlFile.Name = "tbHtmlFile";
            this.tbHtmlFile.Size = new System.Drawing.Size(330, 26);
            this.tbHtmlFile.TabIndex = 1;
            this.tbHtmlFile.TextChanged += new System.EventHandler(this.tbHtmlFile_TextChanged);
            this.tbHtmlFile.Leave += new System.EventHandler(this.tbHtmlFile_Leave);
            //
            // btnChooseHtml
            //
            this.btnChooseHtml.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnChooseHtml.Location = new System.Drawing.Point(532, 36);
            this.btnChooseHtml.Name = "btnChooseHtml";
            this.btnChooseHtml.Size = new System.Drawing.Size(40, 30);
            this.btnChooseHtml.TabIndex = 2;
            this.btnChooseHtml.Text = "...";
            this.btnChooseHtml.UseVisualStyleBackColor = true;
            this.btnChooseHtml.Click += new System.EventHandler(this.btnChooseHtml_Click);
            //
            // btnResetHtml
            //
            this.btnResetHtml.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnResetHtml.Location = new System.Drawing.Point(578, 36);
            this.btnResetHtml.Name = "btnResetHtml";
            this.btnResetHtml.Size = new System.Drawing.Size(92, 30);
            this.btnResetHtml.TabIndex = 3;
            this.btnResetHtml.Text = "Clear";
            this.btnResetHtml.UseVisualStyleBackColor = true;
            this.btnResetHtml.Click += new System.EventHandler(this.btnResetHtml_Click);
            //
            // grpFileTypes
            //
            this.grpFileTypes.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpFileTypes.Controls.Add(this.label5);
            this.grpFileTypes.Controls.Add(this.tbFileExt);
            this.grpFileTypes.Controls.Add(this.btnDefaultFileExt);
            this.grpFileTypes.Controls.Add(this.cbAllowAllExtensions);
            this.grpFileTypes.Controls.Add(this.cbFilesWithNoExt);
            this.grpFileTypes.Location = new System.Drawing.Point(12, 432);
            this.grpFileTypes.Name = "grpFileTypes";
            this.grpFileTypes.Size = new System.Drawing.Size(687, 96);
            this.grpFileTypes.TabIndex = 4;
            this.grpFileTypes.TabStop = false;
            this.grpFileTypes.Text = "File Types";
            //
            // label5
            //
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(15, 33);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(198, 20);
            this.label5.TabIndex = 0;
            this.label5.Text = "Supported file extensions:";
            //
            // tbFileExt
            //
            this.tbFileExt.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbFileExt.Location = new System.Drawing.Point(196, 29);
            this.tbFileExt.Name = "tbFileExt";
            this.tbFileExt.Size = new System.Drawing.Size(330, 26);
            this.tbFileExt.TabIndex = 1;
            this.tbFileExt.TextChanged += new System.EventHandler(this.tbFileExt_TextChanged);
            //
            // btnDefaultFileExt
            //
            this.btnDefaultFileExt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDefaultFileExt.Location = new System.Drawing.Point(578, 27);
            this.btnDefaultFileExt.Name = "btnDefaultFileExt";
            this.btnDefaultFileExt.Size = new System.Drawing.Size(92, 30);
            this.btnDefaultFileExt.TabIndex = 2;
            this.btnDefaultFileExt.Text = "Default";
            this.btnDefaultFileExt.UseVisualStyleBackColor = true;
            this.btnDefaultFileExt.Click += new System.EventHandler(this.btnDefaultFileExt_Click);
            //
            // cbAllowAllExtensions
            //
            this.cbAllowAllExtensions.AutoSize = true;
            this.cbAllowAllExtensions.Location = new System.Drawing.Point(196, 64);
            this.cbAllowAllExtensions.Name = "cbAllowAllExtensions";
            this.cbAllowAllExtensions.Size = new System.Drawing.Size(203, 24);
            this.cbAllowAllExtensions.TabIndex = 3;
            this.cbAllowAllExtensions.Text = "Allow all file extensions";
            this.cbAllowAllExtensions.UseVisualStyleBackColor = true;
            this.cbAllowAllExtensions.CheckedChanged += new System.EventHandler(this.cbAllowAllExtensions_CheckedChanged);
            //
            // cbFilesWithNoExt
            //
            this.cbFilesWithNoExt.AutoSize = true;
            this.cbFilesWithNoExt.Location = new System.Drawing.Point(430, 64);
            this.cbFilesWithNoExt.Name = "cbFilesWithNoExt";
            this.cbFilesWithNoExt.Size = new System.Drawing.Size(246, 24);
            this.cbFilesWithNoExt.TabIndex = 4;
            this.cbFilesWithNoExt.Text = "Preview files without extension";
            this.cbFilesWithNoExt.UseVisualStyleBackColor = true;
            this.cbFilesWithNoExt.CheckedChanged += new System.EventHandler(this.cbFilesWithNoExt_CheckedChanged);
            //
            // grpPanel
            //
            this.grpPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpPanel.Controls.Add(this.cbAutoShowPanel);
            this.grpPanel.Controls.Add(this.cbShowToolbar);
            this.grpPanel.Controls.Add(this.cbShowStatusbar);
            this.grpPanel.Controls.Add(this.cbEnableThreeStateToggle);
            this.grpPanel.Location = new System.Drawing.Point(12, 534);
            this.grpPanel.Name = "grpPanel";
            this.grpPanel.Size = new System.Drawing.Size(687, 104);
            this.grpPanel.TabIndex = 5;
            this.grpPanel.TabStop = false;
            this.grpPanel.Text = "Panel Behavior";
            //
            // cbAutoShowPanel
            //
            this.cbAutoShowPanel.AutoSize = true;
            this.cbAutoShowPanel.Location = new System.Drawing.Point(15, 32);
            this.cbAutoShowPanel.Name = "cbAutoShowPanel";
            this.cbAutoShowPanel.Size = new System.Drawing.Size(311, 24);
            this.cbAutoShowPanel.TabIndex = 0;
            this.cbAutoShowPanel.Text = "Automatically show panel for supported files";
            this.cbAutoShowPanel.UseVisualStyleBackColor = true;
            this.cbAutoShowPanel.CheckedChanged += new System.EventHandler(this.cbAutoShowPanel_CheckedChanged);
            //
            // cbShowToolbar
            //
            this.cbShowToolbar.AutoSize = true;
            this.cbShowToolbar.Location = new System.Drawing.Point(15, 64);
            this.cbShowToolbar.Name = "cbShowToolbar";
            this.cbShowToolbar.Size = new System.Drawing.Size(257, 24);
            this.cbShowToolbar.TabIndex = 1;
            this.cbShowToolbar.Text = "Show toolbar in preview window";
            this.cbShowToolbar.UseVisualStyleBackColor = true;
            this.cbShowToolbar.CheckedChanged += new System.EventHandler(this.cbShowToolbar_Changed);
            //
            // cbShowStatusbar
            //
            this.cbShowStatusbar.AutoSize = true;
            this.cbShowStatusbar.Location = new System.Drawing.Point(360, 32);
            this.cbShowStatusbar.Name = "cbShowStatusbar";
            this.cbShowStatusbar.Size = new System.Drawing.Size(304, 24);
            this.cbShowStatusbar.TabIndex = 2;
            this.cbShowStatusbar.Text = "Show statusbar in preview window (links)";
            this.cbShowStatusbar.UseVisualStyleBackColor = true;
            this.cbShowStatusbar.CheckedChanged += new System.EventHandler(this.cbShowStatusbar_CheckedChanged);
            //
            // cbEnableThreeStateToggle
            //
            this.cbEnableThreeStateToggle.AutoSize = false;
            this.cbEnableThreeStateToggle.Location = new System.Drawing.Point(360, 60);
            this.cbEnableThreeStateToggle.Name = "cbEnableThreeStateToggle";
            this.cbEnableThreeStateToggle.Size = new System.Drawing.Size(312, 40);
            this.cbEnableThreeStateToggle.TabIndex = 3;
            this.cbEnableThreeStateToggle.Text = "Three-state panel toggle (docked \u2192 fullscreen \u2192 hidden)";
            this.cbEnableThreeStateToggle.UseVisualStyleBackColor = true;
            this.cbEnableThreeStateToggle.CheckedChanged += new System.EventHandler(this.cbEnableThreeStateToggle_CheckedChanged);
            //
            // statusStrip1
            //
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sblInvalidHtmlPath});
            this.statusStrip1.Location = new System.Drawing.Point(0, 691);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(711, 22);
            this.statusStrip1.TabIndex = 6;
            this.statusStrip1.Text = "statusStrip1";
            //
            // sblInvalidHtmlPath
            //
            this.sblInvalidHtmlPath.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.sblInvalidHtmlPath.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.sblInvalidHtmlPath.ForeColor = System.Drawing.Color.Red;
            this.sblInvalidHtmlPath.Name = "sblInvalidHtmlPath";
            this.sblInvalidHtmlPath.Size = new System.Drawing.Size(696, 16);
            this.sblInvalidHtmlPath.Spring = true;
            //
            // btnSave
            //
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(484, 648);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(105, 36);
            this.btnSave.TabIndex = 7;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // btnCancel
            //
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(595, 648);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(105, 36);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // SettingsForm
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(711, 713);
            this.Controls.Add(this.grpPanel);
            this.Controls.Add(this.grpFileTypes);
            this.Controls.Add(this.grpExport);
            this.Controls.Add(this.grpPreview);
            this.Controls.Add(this.grpRendering);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.panel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Settings";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.grpRendering.ResumeLayout(false);
            this.grpRendering.PerformLayout();
            this.grpPreview.ResumeLayout(false);
            this.grpPreview.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar1)).EndInit();
            this.grpExport.ResumeLayout(false);
            this.grpExport.PerformLayout();
            this.grpFileTypes.ResumeLayout(false);
            this.grpFileTypes.PerformLayout();
            this.grpPanel.ResumeLayout(false);
            this.grpPanel.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.GroupBox grpRendering;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox comboRenderingEngine;
        private System.Windows.Forms.GroupBox grpPreview;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TrackBar trackBar1;
        private System.Windows.Forms.Label lblZoomValue;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbCssFile;
        private System.Windows.Forms.Button btnChooseCss;
        private System.Windows.Forms.Button btnDefaultCss;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbDarkmodeCssFile;
        private System.Windows.Forms.Button btnChooseDarkmodeCss;
        private System.Windows.Forms.Button btnDefaultDarkmodeCss;
        private System.Windows.Forms.GroupBox grpExport;
        private System.Windows.Forms.Label lblHtmlFile;
        private System.Windows.Forms.TextBox tbHtmlFile;
        private System.Windows.Forms.Button btnChooseHtml;
        private System.Windows.Forms.Button btnResetHtml;
        private System.Windows.Forms.GroupBox grpFileTypes;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbFileExt;
        private System.Windows.Forms.Button btnDefaultFileExt;
        private System.Windows.Forms.CheckBox cbAllowAllExtensions;
        private System.Windows.Forms.CheckBox cbFilesWithNoExt;
        private System.Windows.Forms.GroupBox grpPanel;
        private System.Windows.Forms.CheckBox cbAutoShowPanel;
        private System.Windows.Forms.CheckBox cbShowToolbar;
        private System.Windows.Forms.CheckBox cbShowStatusbar;
        private System.Windows.Forms.CheckBox cbEnableThreeStateToggle;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel sblInvalidHtmlPath;
    }
}
