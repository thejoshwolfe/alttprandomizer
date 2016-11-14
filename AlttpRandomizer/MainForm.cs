using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using AlttpRandomizer.IO;
using AlttpRandomizer.Net;
using AlttpRandomizer.Properties;
using AlttpRandomizer.Random;
using AlttpRandomizer.Rom;
using System.ComponentModel;

namespace AlttpRandomizer
{
    public partial class MainForm : Form
    {
		private Thread checkUpdateThread;
        private BackgroundWorker backgroundWorker;

		public MainForm()
        {
			InitializeSettings();
			InitializeComponent();
		}

		private void InitializeSettings()
		{
			// this fixes an issue with running on wine
		    Settings.Default.SramTrace = Settings.Default.SramTrace;
			Settings.Default.OutputFile = Settings.Default.OutputFile;
			Settings.Default.RandomizerDifficulty = Settings.Default.RandomizerDifficulty;
			Settings.Default.CreateSpoilerLog = Settings.Default.CreateSpoilerLog;
		}

		private void create_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrWhiteSpace(seed.Text))
			{
				SetSeedBasedOnDifficulty();
			}

			ClearOutput();

		    var difficulty = GetRandomizerDifficulty();

		    if (difficulty == RandomizerDifficulty.None)
		    {
		        return;
		    }

            CreateRom(difficulty, false);

		    Settings.Default.SramTrace = sramTrace.Checked;
		    Settings.Default.CreateSpoilerLog = createSpoilerLog.Checked;
			Settings.Default.RandomizerDifficulty = randomizerDifficulty.SelectedItem.ToString();
			Settings.Default.Save();
		}

        private void CreateRom(RandomizerDifficulty difficulty, bool spoilerOnly)
        {
            int parsedSeed;

            if (!int.TryParse(seed.Text, out parsedSeed))
            {
                MessageBox.Show("Seed must be numeric or blank.", "Seed Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                WriteOutput("Seed must be numeric or blank.");
            }
            else
            {
                var romPlms = RomLocationsFactory.GetRomLocations(difficulty);
                RandomizerLog log = null;

                if (spoilerOnly || createSpoilerLog.Checked)
                {
                    log = new RandomizerLog(string.Format(romPlms.SeedFileString, parsedSeed));
                }

                seed.Text = string.Format(romPlms.SeedFileString, parsedSeed);

                var outputString = new StringBuilder();
                backgroundWorker = new BackgroundWorker();
                backgroundWorker.WorkerReportsProgress = true;
                backgroundWorker.WorkerSupportsCancellation = true;
                backgroundWorker.DoWork += (sender, e) =>
                {
                    var randomizer = new Randomizer(parsedSeed, romPlms, log);
                    var options = new RandomizerOptions { Filename = filename.Text, SramTrace = sramTrace.Checked, SpoilerOnly = spoilerOnly };
                    outputString.Append(randomizer.CreateRom(options, backgroundWorker));

                    if (!spoilerOnly)
                    {
                        outputString.AppendFormat("Done!{0}{0}{0}Seed: ", Environment.NewLine);
                        outputString.AppendFormat(romPlms.SeedFileString, parsedSeed);
                        outputString.AppendFormat(" ({0} Difficulty){1}{1}", romPlms.DifficultyName, Environment.NewLine);
                    }
                };
                backgroundWorker.ProgressChanged += (sender, e) =>
                {
                    progressBar.Value = e.ProgressPercentage;
                };
                backgroundWorker.RunWorkerCompleted += (sender, e) =>
                {
                    if (e.Error is OperationCanceledException)
                    {
                        // User cancelled
                        WriteOutput("Cancelled");
                    }
                    else if (e.Error != null)
                    {
                        // Probably a Bug in this app
                        WriteOutput(e.Error.ToString());
                    }
                    else
                    {
                        // Normal completion
                        WriteOutput(outputString.ToString());
                    }
                    SetBackgroundWorkInProgress(false);
                };
                backgroundWorker.RunWorkerAsync();
                SetBackgroundWorkInProgress(true);
            }
        }

        private void SetBackgroundWorkInProgress(bool isInProgress)
        {
            progressBar.Visible = isInProgress;
            cancel.Enabled = isInProgress;
            create.Enabled = !isInProgress;
        }

        private RandomizerDifficulty GetRandomizerDifficulty()
        {
            RandomizerDifficulty difficulty;

            if (seed.Text.ToUpper().Contains("C"))
            {
                randomizerDifficulty.SelectedItem = "Casual";
                seed.Text = seed.Text.ToUpper().Replace("C", "");
                difficulty = RandomizerDifficulty.Casual;
            }
            else
            {
                switch (randomizerDifficulty.SelectedItem.ToString())
                {
                    case "Casual":
                        difficulty = RandomizerDifficulty.Casual;
                        break;
                    default:
                        MessageBox.Show("Please select a difficulty.", "Select Difficulty", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        WriteOutput("Please select a difficulty.");
                        return RandomizerDifficulty.None;
                }
            }

            return difficulty;
        }

        private void SetSeedBasedOnDifficulty()
        {
            switch (randomizerDifficulty.SelectedItem.ToString())
            {
                default:
                    seed.Text = string.Format("C{0:0000000}", (new SeedRandom()).Next(10000000));
                    break;
            }
        }

        private void ClearOutput()
		{
			output.Text = "";
		}

		private void WriteOutput(string text)
		{
			output.Text += text;
		}

		private void browse_Click(object sender, EventArgs e)
		{
			var info = new FileInfo(Regex.Replace(filename.Text, "<.*>", ""));
			var saveFileDialog = new SaveFileDialog { Filter = "All files (*.*)|*.*", FilterIndex = 2, RestoreDirectory = true, InitialDirectory = info.DirectoryName, FileName = info.Name };

			if (saveFileDialog.ShowDialog() == DialogResult.OK)
			{
				filename.Text = saveFileDialog.FileName;
				MessageBox.Show("Remember to hit \"create\" to create the rom.", "Remember to create the rom!", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
			}
		}

		private void filename_TextChanged(object sender, EventArgs e)
		{
			Settings.Default.OutputFile = filename.Text;
			Settings.Default.Save();
		}

		private void filename_Leave(object sender, EventArgs e)
		{
			var senderText = (TextBox)sender;

			if (!senderText.Text.Contains("."))
			{
				senderText.Text += ".sfc";
			}
		}

		private void report_Click(object sender, EventArgs e)
		{
		    if (output.Text.Contains("RandomizationException"))
		    {
                var title = Uri.EscapeUriString(output.Text.Substring(47, output.Text.IndexOf(" --->", StringComparison.Ordinal) - 47));
                var message = Uri.EscapeUriString(output.Text.Substring(0, output.Text.IndexOf("   at", StringComparison.Ordinal)));
                Help.ShowHelp(null, string.Format("https://gitreports.com/issue/Dessyreqt/alttprandomizer?issue_title=[v{0}]%20{1}&details=[v{0}]%0A%0A{2}", RandomizerVersion.CurrentDisplay, title, message));
            }
            else
		    {
                Help.ShowHelp(null, string.Format("https://gitreports.com/issue/Dessyreqt/alttprandomizer?issue_title=[v{0}]%20Anonymous%20Issue&details=[v{0}]%0A%0A", RandomizerVersion.CurrentDisplay));
            }
        }

		private void MainForm_Load(object sender, EventArgs e)
		{
			filename.Text = Settings.Default.OutputFile;
			createSpoilerLog.Checked = Settings.Default.CreateSpoilerLog;
		    sramTrace.Checked = Settings.Default.SramTrace;
			Text = string.Format("A Link to the Past Randomizer v{0}", RandomizerVersion.CurrentDisplay);
			randomizerDifficulty.SelectedItem = Settings.Default.RandomizerDifficulty;
			RunCheckUpdate();

            if (RandomizerVersion.Debug)
            {
                create.BackColor = Color.DarkRed;
                Text += " *** DEBUG ***";
            }
        }

        private void RunCheckUpdate()
		{
			checkUpdateThread = new Thread(RandomizerVersion.CheckUpdate);
			checkUpdateThread.SetApartmentState(ApartmentState.STA);
			checkUpdateThread.Start();
		}

        private void randomSpoiler_Click(object sender, EventArgs e)
        {
            SetSeedBasedOnDifficulty();

            ClearOutput();

            var difficulty = GetRandomizerDifficulty();
            CreateRom(difficulty, true);
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            backgroundWorker.CancelAsync();
        }
    }
}
