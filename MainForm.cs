using System;
using System.Diagnostics;
using System.IO;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Globalization;
using System.Drawing;
using System.Drawing.Text;

/** 
 * ntaiprogrammer 26/01/2022
 */

namespace MollysGameSaveMule
{
    public partial class MainForm : Form
    {
        //Import custom font.
        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbfont, uint cbfont
            , IntPtr pdv, [In] ref uint pcFonts);

        FontFamily ff;
        Font font;

        public MainForm()
        {
            InitializeComponent();
        }

        #region Window Dragging

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        //Click and drag to move the window around.
        //Most form elements plugged into this event.
        private void window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        #endregion Window Dragging

        #region Painting & Rendering

        //Highlighted item colours
        private class MyRenderer : ToolStripProfessionalRenderer
        {
            public MyRenderer() : base(new MyColors()) { }
        }

        private class MyColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected
            {
                get { return Color.DarkOrange; }
            }
            public override Color MenuItemSelectedGradientBegin
            {
                get { return Color.White; }
            }
            public override Color MenuItemSelectedGradientEnd
            {
                get { return Color.White; }
            }
            public override Color MenuItemBorder //ITEM BORDER WAS BLUE
            {
                get { return Color.Orange; }
            }
        }

        //All this to change the groupbox border colour.
        private void GroupBox_Paint(object sender, PaintEventArgs e)
        {
            GroupBox box = sender as GroupBox;
            DrawGroupBox(box, e.Graphics, Color.Orange, Color.Orange);
        }

        private void DrawGroupBox(GroupBox box, Graphics g, Color textColor, Color borderColor)
        {
            if (box != null)
            {
                Brush textBrush = new SolidBrush(textColor);
                Brush borderBrush = new SolidBrush(borderColor);
                Pen borderPen = new Pen(borderBrush);
                SizeF strSize = g.MeasureString(box.Text, box.Font);
                Rectangle rect = new Rectangle(box.ClientRectangle.X,
                                               box.ClientRectangle.Y + (int)(strSize.Height / 2),
                                               box.ClientRectangle.Width - 1,
                                               box.ClientRectangle.Height - (int)(strSize.Height / 2) - 1);

                // Clear text and border
                g.Clear(this.BackColor);
                g.Clear(this.ForeColor); //CHANGED FROM CLEAR BACKCOLOUR, REMOVED GREY BACKGROUND FOR GROUPBOX

                // Draw text
                g.DrawString(box.Text, box.Font, textBrush, box.Padding.Left, 0);

                // Drawing Border
                //Left
                g.DrawLine(borderPen, rect.Location, new Point(rect.X, rect.Y + rect.Height));
                //Right
                g.DrawLine(borderPen, new Point(rect.X + rect.Width, rect.Y), new Point(rect.X + rect.Width, rect.Y + rect.Height));
                //Bottom
                g.DrawLine(borderPen, new Point(rect.X, rect.Y + rect.Height), new Point(rect.X + rect.Width, rect.Y + rect.Height));
                //Top1
                g.DrawLine(borderPen, new Point(rect.X, rect.Y), new Point(rect.X + box.Padding.Left, rect.Y));
                //Top2
                g.DrawLine(borderPen, new Point(rect.X + box.Padding.Left + (int)(strSize.Width), rect.Y), new Point(rect.X + rect.Width, rect.Y));
            }
        }

        #endregion Painting & Rendering

        #region Variables

        //Shows normal file explorer window when selecting folders, instead of the awful FolderBrowserDialog.
        CommonOpenFileDialog dialog = new CommonOpenFileDialog();

        DialogResult dialogResultCustom;

        //For detecting special characters, to locate which file is the save file in the default xbox folder.
        Regex rgx = new Regex("[^A-Za-z0-9]");

        //File size difference between steam and xbox save files.
        long sizeDifference;

        //Save detection.
        bool steamSaveDetected = false;
        bool xboxSaveDetected = false;

        //Backup detection.
        bool steamBackupDetected = false;
        bool xboxBackupDetected = false;

        //Whether user settings have been read successfully.
        bool settingsRead = false;

        string mostRecentSaveFilePath = null;
        string largestFileSizeFilePath = null;

        //Capitalised for formatting when used in display messages.
        string steam = "Steam";
        string xbox = "Xbox";
        string backup = "Backup";
        string both = "Both";

        #region Messages

        //Save detection messages.
        readonly string refreshFileDetectionMessage = "To re-attempt file detection, click Set Folders > Refresh File Detection";
        readonly string bothSavesNotPresentMessage = "Save(s) not found";

        readonly string steamSaveDetectedMessage = "Show Steam Save";
        readonly string xboxSaveDetectedMessage = "Show Xbox Save";

        readonly string steamSaveNotDetectedMessage = "Steam save not detected";
        readonly string xboxSaveNotDetectedMessage = "Xbox save not detected";

        //Backup detection messages.
        readonly string steamBackupDetectedMessage = "Show Steam Backup";
        readonly string xboxBackupDetectedMessage = "Show Xbox Backup";

        readonly string steamBackupNotDetectedMessage = "Steam backup not detected";
        readonly string xboxBackupNotDetectedMessage = "Xbox backup not detected";

        //Backup messages
        readonly string backupSuccessfulMessage = "New backups created.";
        readonly string backupFailedMessage = "Backup failed: One or more saves not detected. " +
            "\nTry setting folder locations manually.";

        //Backup restoration messages
        readonly string restoredFromBackupMessage = "File(s) restored from backup.";
        readonly string restoreFailedMessage = "Restore failed, backup not detected.";

        #endregion Messages

        #region File Paths

        //File paths for xbox container file and it's backup.
        static string xboxFilePathContainer = "";
        static string xboxFilePathBackupContainer = "";

        //Path to store program's files.
        static string mollysGameSaveMuleProgramFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Molly's Game-Save Mule";

        //Path to store user settings.
        static string settingsFilePath = mollysGameSaveMuleProgramFolder + @"/settings.txt";

        //Default backup folder path.
        static string backupFolderPathDefault = mollysGameSaveMuleProgramFolder + @"\Backups";

        //Default steam save folder path.
        static string steamFolderPathDefault = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) +
            @"\Steam\steamapps\common\Deep Rock Galactic\FSD\Saved\SaveGames";

        /** 
         * Default xbox save folder path. 
         * Location 2 folders above default xbox save folder. 
         * Folder names beyond this point vary between users, 
         * and must be narrowed down further.
         */
        static string xboxFolderPathDefault = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            + @"\Packages\CoffeeStainStudios.DeepRockGalactic_496a1srhmar9w\SystemAppData\wgs";

        //Initialize file paths to default values.
        static string backupFolderPath = backupFolderPathDefault;
        static string steamFolderPath = steamFolderPathDefault;
        static string xboxFolderPath = xboxFolderPathDefault;

        #endregion File Paths

        #endregion Variables

        #region Methods

        #region Locating Files

        //Selects and executes relevant file location method.
        public void LocateFile(string steamOrXbox, bool backupOrNot)
        {
            //Disable transfer buttons.
            btn_SteamToXbox.Enabled = false;
            btn_XboxToSteam.Enabled = false;

            string folderPath;

            if (steamOrXbox.Equals(steam))
            {
                if (backupOrNot == true)
                {
                    folderPath = backupFolderPath;
                }
                else
                {
                    folderPath = steamFolderPath;
                }

                FindSteam(folderPath);
            }
            else if (steamOrXbox.Equals(xbox))
            {
                if (backupOrNot == true)
                {
                    folderPath = backupFolderPath;
                }
                else
                {
                    folderPath = xboxFolderPath;
                }

                FindXbox(folderPath);
            }

            if (steamSaveDetected == true && xboxSaveDetected == true)
            {
                //If both saves detected, enable transfer buttons and update button label.
                btn_AutoSync.Text = "Sync Most Progress";
                btn_AutoSync.Enabled = true;
                btn_SteamToXbox.Enabled = true;
                btn_XboxToSteam.Enabled = true;
            }
            else
            {
                label_StatusInfo.Text = refreshFileDetectionMessage;
            }
        }

        //Find the main save file (Steam) among the backups and others in default folder.
        public void FindSteam(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                return;
            }

            //Get files from steam save folder
            string[] files = Directory.GetFiles(folderPath);
            foreach (string file in files)
            {
                /**
                 * Search for the Player.sav file and set the file path.
                 * Check if filename contains "player" but not "external".
                 * This narrows it down to the main save file. Folder contains several backups and others.
                 */
                if (Path.GetFileName(file).IndexOf("external", StringComparison.OrdinalIgnoreCase) <= 0
                    && Path.GetFileName(file).IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    //If not a backup
                    if (!folderPath.Equals(backupFolderPath))
                    {
                        //Set file path
                        steamFilePath = Path.GetFullPath(file);
                        steamSaveDetected = true;
                    }
                    else
                    {
                        //Set backup file path
                        steamFilePathBackup = Path.GetFullPath(file);

                        if (File.Exists(steamFilePathBackup))
                        {
                            steamBackupDetected = true;
                        }
                    }
                    //Exit foreach loop
                    break;
                }
            }
            UpdateSaveDetectionStatusLabel();
        }

        //Finds xbox save file, sets paths for file and containing folder if default.
        public void FindXbox(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                return;
            }

            //Search in first directory if user-defined folder.
            FindXboxIndentify(folderPath);

            //Exit if found.
            if (xboxSaveDetected == true)
            {
                return;
            }

            /** 
             * Search subfolders if not found. 
             * All subfolders into array, check each name for "0" 
             * to narrow down results to correct folder.
             * There should only be 2 levels.
             */
            string[] xboxFolders = Directory.GetDirectories(folderPath);
            foreach (string xboxFolder in xboxFolders)
            {
                //Nested IF to navigate several subfolders named with random numbers
                if (xboxFolder.Contains("0"))
                {
                    //Subfolder's subfolders into array, check each name for "0" to narrow down results further.
                    string[] xboxSubFolders = Directory.GetDirectories(xboxFolder);
                    foreach (string folder in xboxSubFolders)
                    {
                        if (Path.GetDirectoryName(folder).Contains("0"))
                        {
                            //Reattempt locate function
                            FindXboxIndentify(folder);
                        }
                    }
                }
                else
                {
                    continue; //Next element
                }
            }
        }

        /** 
         * Method to locate xbox save file. Searches within current folder. 
         * Called from larger method at different stages to perform search with or without
         * the sub-folders typically found in default xbox gamepass save location.
         */
        public void FindXboxIndentify(string folderPath)
        {
            bool saveFound = false;
            bool containerFound = false;

            string[] files = Directory.GetFiles(folderPath);
            foreach (string file in files)
            {
                //Use regular expression to detect special characters in file name, which the xbox save file won't have.
                bool containsSpecialCharacter = rgx.IsMatch(file);

                //If file name contains "container", set container file path
                if (Path.GetFileName(file).Contains("container"))
                {
                    if (folderPath.Equals(backupFolderPath))
                    {
                        xboxFilePathBackupContainer = file;
                    }
                    else
                    {
                        xboxFilePathContainer = file;
                    }
                    containerFound = true;
                }
                else
                {
                    //check for steam file identifiers
                    if (containsSpecialCharacter == true
                        && !Path.GetFileName(file).Contains("Player")
                        && !Path.GetFileName(file).Contains("steam")
                        && !Path.GetFileName(file).Contains("Backup"))
                    {
                        //If backup
                        if (folderPath.Equals(backupFolderPath))
                        {
                            xboxFilePathBackup = Path.GetFullPath(file);
                            xboxBackupDetected = true;
                        }
                        else
                        {
                            //Set file path
                            xboxFilePath = Path.GetFullPath(file);
                            xboxSaveDetected = true;
                        }
                        saveFound = true;
                    }
                }

                //Exit loop if both save file and container file are found.
                if (saveFound == true && containerFound == true)
                {
                    break;
                }
            }
        }

        #endregion Locating Files

        #region File Transfer

        //Creates dynamic confirmation text before a file transfer.
        public string ConfirmationMessageSaveTransfer(string steamOrXbox)
        {
            string source = "";
            string destination = "";

            if (steamOrXbox.Equals(steam))
            {
                source = steam;
                destination = xbox;
            }
            else if (steamOrXbox.Equals(xbox))
            {
                source = xbox;
                destination = steam;
            }

            string confirmationMessageSaveTransfer = source + " save file will overwrite " + destination + " save file." +
                "\n\nBackups of both saves will be " +
                "\nautomatically created before the transfer." +
                "\n\nThe transfer can be reversed by selecting " +
                "\n\"Restore From Backup\" > \"Restore " + destination + "\".";

            return confirmationMessageSaveTransfer;
        }

        //Overwrites one save file with the other, renames file accordingly.
        public void TransferSave(string steamOrXboxSource)
        {
            string source = "";
            string destination = "";

            string sourceDisplay = "";
            string destinationDisplay = "";

            CreateBackups();

            //If steam chosen as source file
            if (steamOrXboxSource.Equals(steam))
            {
                //Set filepaths for source and destination
                source = steamFilePath;
                destination = xboxFilePath;

                //Set display strings to correct items
                sourceDisplay = steam;
                destinationDisplay = xbox;
            }
            //If xbox chosen as source file
            else if (steamOrXboxSource.Equals(xbox))
            {
                //Set filepaths for source and destination
                source = xboxFilePath;
                destination = steamFilePath;

                //Set display strings to correct items
                sourceDisplay = xbox;
                destinationDisplay = steam;
            }

            if (steamFilePath != "" && xboxFilePath != "")
            {
                File.Copy(source, destination, true);
                label_StatusInfo.Text = sourceDisplay + " save copied to " + destinationDisplay + ".";
            }
            else
            {
                CustomMessageBox("Information", backupFailedMessage, false, null);
            }
        }

        /** 
         * Automatically determines best save file based on file size and last write time.
         * File size is prioritised, since it is more indicative of further game progress.
         * If sizes are similar (less than 10KB difference), the save with most recent file write is chosen.
         * User is told which save is being chosen and why. User must then confirm or cancel the transfer.
         */
        public void AutoSyncSaveFiles()
        {
            //Determine largest save file
            FileInfo fileInfoSteam = new FileInfo(steamFilePath);
            FileInfo fileInfoXbox = new FileInfo(xboxFilePath);

            long steamFileSize = fileInfoSteam.Length;
            long xboxFileSize = fileInfoXbox.Length;

            //Get absolute value (treats negative numbers as positive)
            sizeDifference = Math.Abs(steamFileSize - xboxFileSize);

            if (sizeDifference < 10000)
            {
                largestFileSizeFilePath = null;
            }
            else if (steamFileSize > xboxFileSize)
            {
                largestFileSizeFilePath = steamFilePath;
            }
            else if (xboxFileSize > steamFileSize)
            {
                largestFileSizeFilePath = xboxFilePath;
            }

            //Determine save file with most recent file write
            DateTime steamDateTime = File.GetLastWriteTime(steamFilePath);
            DateTime xboxDateTime = File.GetLastWriteTime(xboxFilePath);

            if (steamDateTime > xboxDateTime)
            {
                mostRecentSaveFilePath = steamFilePath;
            }
            else if (xboxDateTime > steamDateTime)
            {
                mostRecentSaveFilePath = xboxFilePath;
            }
            else if (xboxDateTime == steamDateTime)
            {
                mostRecentSaveFilePath = null;
            }

            //Determine whether each file is steam or xbox
            string largestFile = "";

            if (largestFileSizeFilePath != null)
            {
                if (largestFileSizeFilePath.Contains("Player"))
                {
                    largestFile = steam;
                }
                else
                {
                    largestFile = xbox;
                }
            }

            string mostRecentFile = "";

            if (mostRecentSaveFilePath != null)
            {
                if (mostRecentSaveFilePath.Contains("Player"))
                {
                    mostRecentFile = steam;
                }
                else
                {
                    mostRecentFile = xbox;
                }
            }

            /**
             * Prioritise larger file size.
             * Defer to most recent file write if similar size. 
             * Message if both file size and last write are similar.
             */

            string message;

            //Tell user that larger file is being chosen, seek confirmation.
            if (largestFileSizeFilePath != null)
            {
                message = "Save to be synced: " + largestFile +
                    "\n\nReason: Your " + largestFile + " save is " + sizeDifference + " bytes larger." +
                    "\n\nLarger file size is a better indicator of " +
                    "\ngame progress than most recent file write." +
                    "\n\n" + ConfirmationMessageSaveTransfer(largestFile) +
                    "\n\nIf this choice doesn't seem right, " +
                    "\nclick \"No\" and open each save file in-game " +
                    "\nto verify which one you wish to sync.";

                CustomMessageBox("Confirmation", message, true, largestFile);
                if (dialogResultCustom == DialogResult.Yes)
                {
                    TransferSave(largestFile);
                }
            }
            //Tell user most recent file being chosen, seek confirmation.
            else if (largestFileSizeFilePath == null && mostRecentSaveFilePath != null)
            {
                message = "Save to be synced: " + mostRecentFile +
                    "\n\nReason: File sizes are similar but " + mostRecentFile +
                    "\n has the most recent changes." +
                    "\n\n" + ConfirmationMessageSaveTransfer(mostRecentFile) +
                    "\n\nIf this choice doesn't seem right, " +
                    "\nclick \"No\" and open each save file in-game " +
                    "\nto verify which one you wish to sync.";

                CustomMessageBox("Confirmation", message, true, mostRecentFile);
                if (dialogResultCustom == DialogResult.Yes)
                {
                    TransferSave(mostRecentFile);
                }
            }
            //If both size and last write time are too similar, tell user to check save files in-game.
            else if (largestFileSizeFilePath == null && mostRecentSaveFilePath == null)
            {
                message = "Could not determine most recent save." +
                    "\n\nBoth saves have similar file sizes and " +
                    "\nwere last modified at the same time." +
                    "\n\nThis probably means your saves are already synced." +
                    "\n\nIf you just closed the game a moment ago, " +
                    "\nwait a few seconds and try again." +
                    "\n\nIf this doesn't seem right, open each save file " +
                    "\nin-game to verify which one you wish to sync.";

                CustomMessageBox("Information", message, false, null);
            }
        }

        #endregion File Transfer

        #region Backups

        public void CreateBackups()
        {
            //Create backup folder if not already existing.
            if (!Directory.Exists(backupFolderPath))
            {
                Directory.CreateDirectory(backupFolderPath);
            }

            //Move previous backups to dated archive folder before creating new ones
            string currentDatetime = DateTime.Now.ToString("dd.MM.yyyy_hh.mm.sstt", CultureInfo.InvariantCulture);
            string[] oldBackups = Directory.GetFiles(backupFolderPath);

            if (oldBackups.Length > 0)
            {
                string newArchiveFolder = "";
                string pathArchive = backupFolderPath + @"\Archived_" + @currentDatetime;

                if (!Directory.Exists(pathArchive))
                {
                    DirectoryInfo a = Directory.CreateDirectory(pathArchive);
                    newArchiveFolder = a.FullName;
                }

                foreach (string backup in oldBackups)
                {
                    string newPath = newArchiveFolder + @"\" + Path.GetFileName(backup);
                    if (!File.Exists(newPath))
                    {
                        File.Move(Path.GetFullPath(backup), newPath);
                    }
                }
            }

            Backup(steamFilePath);
            btn_LocateSteamBackup.Enabled = true;
            steamBackupDetected = true;

            Backup(xboxFilePath);
            Backup(xboxFilePathContainer); //Container file, only xbox has this.
            btn_LocateXboxBackup.Enabled = true;
            xboxBackupDetected = true;

            UpdateBackupStatusLabel();
        }

        //Creates a backup. Called from CreateBackups method.
        public void Backup(string original)
        {
            if (original != null && backup != null)
            {
                //Set backup file path using string builder method
                string newBackup = BackupFilePathStringBuilder(original);

                //Copy to backup file
                File.Copy(original, newBackup, true);
            }

            //Check for files and update backup detection status.
            if (File.Exists(steamFilePathBackup))
            {
                steamBackupDetected = true;
            }

            if (File.Exists(xboxFilePathBackup))
            {
                xboxBackupDetected = true;
            }
        }

        //Restores selected files from backup.
        public void RestoreBackups(bool steam, bool xbox)
        {
            //Exit method if no backups found. Avoids multiple message boxes.
            if ((!File.Exists(steamFilePathBackup)) && (!File.Exists(xboxFilePathBackup)))
            {
                CustomMessageBox("Information", restoreFailedMessage, false, null);
                return;
            }

            if (steam == true)
            {
                if (File.Exists(steamFilePathBackup))
                {
                    File.Copy(steamFilePathBackup, steamFilePath, true);
                    label_StatusInfo.Text = restoredFromBackupMessage;
                }
                else
                {
                    CustomMessageBox("Information", restoreFailedMessage, false, null);
                }
            }

            if (xbox == true)
            {
                if (File.Exists(xboxFilePathBackup))
                {
                    File.Copy(xboxFilePathBackup, xboxFilePath, true);
                    File.Copy(xboxFilePathBackupContainer, xboxFilePathContainer, true); //Container file, only xbox has this.
                    label_StatusInfo.Text = restoredFromBackupMessage;
                }
                else
                {
                    CustomMessageBox("Information", restoreFailedMessage, false, null);
                }
            }
        }

        //Builds file path string by appending filename to backup folder path.
        public string BackupFilePathStringBuilder(string steamOrXboxFileName)
        {
            string backupFilePath = backupFolderPath + @"\" + Path.GetFileName(steamOrXboxFileName);
            return backupFilePath;
        }

        #endregion Backups

        #region User Settings

        //Read settings from file
        public void GetSettings(string file)
        {
            if (File.Exists(file))
            {
                string[] settings = File.ReadAllLines(file);

                if (!string.IsNullOrEmpty(settings[0])
                    && !string.IsNullOrEmpty(settings[1])
                    && !string.IsNullOrEmpty(settings[2]))
                {
                    backupFolderPath = settings[0];
                    steamFolderPath = settings[1];
                    xboxFolderPath = settings[2];

                    settingsRead = true;
                }
                else
                {
                    settingsRead = false;
                }
            }
            else
            {
                settingsRead = false;
            }
        }

        //Write settings to file
        public void SaveSettings()
        {
            using (StreamWriter writer = new StreamWriter(settingsFilePath, false)) //Append set to false, to overwrite the file each time
            {
                writer.WriteLine(backupFolderPath);
                writer.WriteLine(steamFolderPath);
                writer.WriteLine(xboxFolderPath);
            }
            label_StatusInfo.Text = "Settings saved.";
        }

        #endregion User Settings

        #region Path-Changed Events

        //EVENTS WHEN FILE PATHS CHANGE
        //Updates detection status of saves and backups when file paths change
        private string _steamFilePath;

        public event System.EventHandler SteamPathChanged;

        protected virtual void OnSteamPathChanged()
        {
            if (SteamPathChanged != null) SteamPathChanged(this, EventArgs.Empty);
            if (File.Exists(steamFilePath))
            {
                btn_LocateSteamSave.Enabled = true;
                btn_LocateSteamSave.Text = steamSaveDetectedMessage;
            }
            else
            {
                btn_LocateSteamSave.Enabled = false;
                btn_LocateSteamSave.Text = steamSaveNotDetectedMessage;
            }
        }

        public string steamFilePath
        {
            get
            {
                return _steamFilePath;
            }
            set
            {
                _steamFilePath = value;
                OnSteamPathChanged();
            }
        }

        private string _steamFilePathBackup;

        public event System.EventHandler SteamPathBackupChanged;

        protected virtual void OnSteamPathBackupChanged()
        {
            if (SteamPathBackupChanged != null) SteamPathBackupChanged(this, EventArgs.Empty);
            if (File.Exists(steamFilePathBackup))
            {
                btn_LocateSteamBackup.Enabled = true;
                btn_LocateSteamBackup.Text = steamBackupDetectedMessage;
            }
        }

        public string steamFilePathBackup
        {
            get
            {
                return _steamFilePathBackup;
            }
            set
            {
                _steamFilePathBackup = value;
                OnSteamPathBackupChanged();
            }
        }

        private string _xboxFilePath;

        public event System.EventHandler XboxPathChanged;

        protected virtual void OnXboxPathChanged()
        {
            if (XboxPathChanged != null) XboxPathChanged(this, EventArgs.Empty);
            if (File.Exists(xboxFilePath))
            {
                btn_LocateXboxSave.Enabled = true;
                btn_LocateXboxSave.Text = xboxSaveDetectedMessage;
            }
            else
            {
                btn_LocateXboxSave.Enabled = false;
                btn_LocateXboxSave.Text = xboxSaveNotDetectedMessage;
            }
        }

        public string xboxFilePath
        {
            get
            {
                return _xboxFilePath;
            }
            set
            {
                _xboxFilePath = value;
                OnXboxPathChanged();
            }
        }

        private string _xboxFilePathBackup;

        public event System.EventHandler XboxPathBackupChanged;

        protected virtual void OnXboxPathBackupChanged()
        {
            if (XboxPathBackupChanged != null) XboxPathBackupChanged(this, EventArgs.Empty);
            if (File.Exists(xboxFilePathBackup))
            {
                btn_LocateXboxBackup.Enabled = true;
                btn_LocateXboxBackup.Text = xboxBackupDetectedMessage;
            }
        }

        public string xboxFilePathBackup
        {
            get
            {
                return _xboxFilePathBackup;
            }
            set
            {
                _xboxFilePathBackup = value;
                OnXboxPathBackupChanged();
            }
        }

        #endregion Path-Changed Events

        #region Font

        //Get font from resources.
        private void LoadFont()
        {
            byte[] fontArray = MollysGameSaveMule.Properties.Resources.Heavitas;
            int dataLength = MollysGameSaveMule.Properties.Resources.Heavitas.Length;

            IntPtr ptrData = Marshal.AllocCoTaskMem(dataLength);

            Marshal.Copy(fontArray, 0, ptrData, dataLength);

            uint cFonts = 0;

            AddFontMemResourceEx(ptrData, (uint)fontArray.Length, IntPtr.Zero, ref cFonts);

            PrivateFontCollection pfc = new PrivateFontCollection();

            pfc.AddMemoryFont(ptrData, dataLength);

            Marshal.FreeCoTaskMem(ptrData);

            ff = pfc.Families[0];
            font = new Font(ff, 15f, FontStyle.Regular);
        }

        //Apply font to control item.
        private void AllocateFont(Font f, Control c, float size)
        {
            FontStyle fontStyle = FontStyle.Regular;
            c.Font = new Font(ff, size, fontStyle);
        }

        //Same as allocate font, but for tool strip menu items.
        private void AllocateFontTSMenuItem(Font f, ToolStripMenuItem tsmi, float size)
        {
            FontStyle fontStyle = FontStyle.Regular;
            tsmi.Font = new Font(ff, size, fontStyle);
        }

        #endregion Font

        #region Other

        private void MainForm_Load(object sender, EventArgs e)
        {
            //Load font from embedded resource and allocate font to all controls/elements.
            LoadFont();

            AllocateFont(font, label_Title, 16);
            AllocateFont(font, btn_Minimize, 12);
            AllocateFont(font, btn_Close, 12);
            AllocateFontTSMenuItem(font, setFoldersToolStripMenuItem, 9);
            AllocateFontTSMenuItem(font, backupToolStripMenuItem, 9);
            AllocateFontTSMenuItem(font, restoreBackupsToolStripMenuItem, 9);
            AllocateFontTSMenuItem(font, helpToolStripMenuItem, 9);
            AllocateFont(font, gb_Transfer, 9);
            AllocateFont(font, gb_Saves, 9);
            AllocateFont(font, gb_Backups, 9);
            AllocateFont(font, btn_AutoSync, 9);
            AllocateFont(font, btn_SteamToXbox, 9);
            AllocateFont(font, btn_XboxToSteam, 9);
            AllocateFont(font, btn_LocateSteamSave, 9);
            AllocateFont(font, btn_LocateXboxSave, 9);
            AllocateFont(font, btn_LocateSteamBackup, 9);
            AllocateFont(font, btn_LocateXboxBackup, 9);
            AllocateFont(font, label_StatusInfo, 9);

            OnStart();
        }

        //Startup ritual. Check for user settings, apply some cosmetics, locate files.
        public void OnStart()
        {
            //Create folder for program in MyDocuments if it doesn't exist.
            if (!Directory.Exists(mollysGameSaveMuleProgramFolder))
            {
                Directory.CreateDirectory(mollysGameSaveMuleProgramFolder);
            }

            //Look for existing user settings.
            GetSettings(settingsFilePath);

            //Settings to default if user settings file non-existent or unreadable.
            if (settingsRead == false)
            {
                using (StreamWriter writer = new StreamWriter(settingsFilePath, false)) //Will append if true.
                {
                    writer.WriteLine(backupFolderPathDefault);
                    writer.WriteLine(steamFolderPathDefault);
                    writer.WriteLine(xboxFolderPathDefault);
                }
            }

            //MAKES GROUPBOX BORDERS CUSTOM COLOUR.
            gb_Transfer.Paint += GroupBox_Paint;
            gb_Saves.Paint += GroupBox_Paint;
            gb_Backups.Paint += GroupBox_Paint;

            //Custom colours for menu strip highlighted items.
            menuStrip_Main.Renderer = new MyRenderer();

            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            dialog.RestoreDirectory = true;

            //Locate saves.
            LocateFile(steam, false);
            LocateFile(xbox, false);

            //Locate backups.
            LocateFile(steam, true);
            LocateFile(xbox, true);

            //Create backups if none exist.
            if (File.Exists(steamFilePath) && File.Exists(xboxFilePath))
            {
                if (steamBackupDetected == false && xboxBackupDetected == false)
                {
                    string message = "No backups detected in the selected folder." +
                    "\n\nWould you like to create new backups there?";

                    CustomMessageBox("Confirmation", message, true, null);
                    if (dialogResultCustom == DialogResult.Yes)
                    {
                        //Create backups in selected folder for both steam and xbox.
                        CreateBackups();
                    }
                }
            }

            SaveSettings();

            //Show welcome message if both saves found, show refresh instructions if not.
            if (steamSaveDetected == true && xboxSaveDetected == true)
            {
                label_StatusInfo.Text = "\"At least we don't have to haul all these save files around ourselves!\"";
            }
            else
            {
                label_StatusInfo.Text = refreshFileDetectionMessage;
            }
        }

        //Opens the relevant folder and highlights the save file based on parameter passed at click event.
        //Additional parameters (steamOrXbox, backupOrNot) are for resetting detection status of correct file.
        public void ShowFile(string filePath, string steamOrXbox, bool backupOrNot)
        {
            if (!File.Exists(filePath))
            {
                //Reset save or backup detection status if files moved or missing after last check
                if (backupOrNot == true)
                {
                    ResetBackupDetectionVariables(steamOrXbox);
                    UpdateBackupStatusLabel();
                }
                else
                {
                    ResetSaveDetectionVariable(steamOrXbox);
                    UpdateSaveDetectionStatusLabel();
                }

                LocateFile(steamOrXbox, backupOrNot);
            }

            //Shows the file
            Process.Start("explorer.exe", "/select, " + filePath);
        }

        //Use ref keyword to avoid "file in use by another process" error.
        public void SetFolder(ref string folder, bool backupOrNot, string steamOrXbox)
        {
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                //Set backup folder to user selection.
                folder = dialog.FileName;

                if (backupOrNot == true)
                {
                    btn_LocateSteamBackup.Enabled = false;
                    btn_LocateXboxBackup.Enabled = false;

                    btn_LocateSteamBackup.Text = steamBackupNotDetectedMessage;
                    btn_LocateXboxBackup.Text = xboxBackupNotDetectedMessage;

                    ResetBackupDetectionVariables(both);

                    //Search for backup saves.
                    LocateFile(steam, backupOrNot);
                    LocateFile(xbox, backupOrNot);

                    UpdateBackupStatusLabel();

                    //Show message if no backups detected.
                    if (steamBackupDetected == false && xboxBackupDetected == false)
                    {
                        string message = "No backups detected in the selected folder." +
                        "\n\nWould you like to create new backups there?";

                        CustomMessageBox("Confirmation", message, true, null);
                        if (dialogResultCustom == DialogResult.Yes)
                        {
                            //Create backups in selected folder for both steam and xbox.
                            CreateBackups();
                        }
                    }
                }
                else
                {
                    btn_SteamToXbox.Enabled = false;
                    btn_XboxToSteam.Enabled = false;

                    //Disable relevant "show save" button
                    ResetShowSaveButtonToFalse(steamOrXbox);

                    //Reset relevant save detection status
                    ResetSaveDetectionVariable(steamOrXbox);

                    btn_AutoSync.Text = bothSavesNotPresentMessage;
                    btn_AutoSync.Enabled = false;

                    //Detect save file
                    LocateFile(steamOrXbox, backupOrNot);
                }
                SaveSettings();
            }
        }

        //Creates dynamic custom messagebox.
        public void CustomMessageBox(string title, string text, bool yesNo, string steamOrXbox)
        {
            messageBoxForm customMessageBox = new messageBoxForm();
            dialogResultCustom = customMessageBox.ShowCustomDialog(title, text, yesNo);
        }

        //Resets save detection values to false.
        //Uses parameter to select steam or xbox value.
        public void ResetSaveDetectionVariable(string steamOrXbox)
        {
            if (steamOrXbox.Equals(steam))
            {
                steamSaveDetected = false;
            }
            else if (steamOrXbox.Equals(xbox))
            {
                xboxSaveDetected = false;
            }
        }

        //Resets all backup file detection values.
        public void ResetBackupDetectionVariables(string steamOrXbox)
        {
            if (steamOrXbox.Equals(steam))
            {
                steamBackupDetected = false;
            }
            else if (steamOrXbox.Equals(xbox))
            {
                xboxBackupDetected = false;
            }
            else if (steamOrXbox.Equals(both))
            {
                steamBackupDetected = false;
                xboxBackupDetected = false;
            }
        }

        //Disables selected save button, updates button label.
        public void ResetShowSaveButtonToFalse(string steamOrXbox)
        {
            if (steamOrXbox.Equals(steam))
            {
                btn_LocateSteamSave.Enabled = false;
                btn_LocateSteamSave.Text = steamSaveNotDetectedMessage;
            }
            else if (steamOrXbox.Equals(xbox))
            {
                btn_LocateXboxSave.Enabled = false;
                btn_LocateXboxSave.Text = xboxSaveNotDetectedMessage;
            }
        }

        public void SaveFilesNotFoundMessageBox()
        {
            string message = "One or more save files not found." +
                    "\nTry setting folder locations manually.";

            CustomMessageBox("Information", message, false, null);
        }

        //Update backup save file detection message.
        //Used when checking for file while not making any changes.
        public void UpdateBackupStatusLabel()
        {
            string steamBackupStatus;
            string xboxBackupStatus;

            if (steamBackupDetected == true)
            {
                steamBackupStatus = steamBackupDetectedMessage;
            }
            else
            {
                steamBackupStatus = steamBackupNotDetectedMessage;
            }

            //Display steam message
            btn_LocateSteamBackup.Text = steamBackupStatus;

            if (xboxBackupDetected == true)
            {
                xboxBackupStatus = xboxBackupDetectedMessage;
            }
            else
            {
                xboxBackupStatus = xboxBackupNotDetectedMessage;
            }

            //Display xbox message
            btn_LocateXboxBackup.Text = xboxBackupStatus;
        }

        //Update save file detection status message.
        public void UpdateSaveDetectionStatusLabel()
        {
            //Determine steam message
            if (steamSaveDetected == true)
            {
                btn_LocateSteamSave.Text = steamSaveDetectedMessage;
            }
            else
            {
                btn_LocateSteamSave.Text = steamSaveNotDetectedMessage;
            }

            //Determine xbox message
            if (xboxSaveDetected == true)
            {
                btn_LocateXboxSave.Text = xboxSaveDetectedMessage;
            }
            else
            {
                btn_LocateXboxSave.Text = xboxSaveNotDetectedMessage;
            }
        }

        #endregion Other

        #endregion Methods

        #region Controls

        #region Buttons

        private void btn_AutoSync_Click(object sender, EventArgs e)
        {
            AutoSyncSaveFiles();
        }

        private void btn_SteamToXbox_Click(object sender, EventArgs e)
        {
            CustomMessageBox("Confirmation", ConfirmationMessageSaveTransfer(steam), true, steam);

            if (dialogResultCustom == DialogResult.Yes)
            {
                TransferSave(steam);
            }
        }

        private void btn_XboxToSteam_Click(object sender, EventArgs e)
        {
            CustomMessageBox("Confirmation", ConfirmationMessageSaveTransfer(xbox), true, xbox);

            if (dialogResultCustom == DialogResult.Yes)
            {
                TransferSave(xbox);
            }
        }

        private void btn_LocateSteamSave_Click(object sender, EventArgs e)
        {
            ShowFile(steamFilePath, steam, false);
        }

        private void btn_LocateXboxSave_Click(object sender, EventArgs e)
        {
            ShowFile(xboxFilePath, xbox, false);
        }

        private void btn_LocateSteamBackup_Click(object sender, EventArgs e)
        {
            ShowFile(steamFilePathBackup, steam, true);
        }

        private void btn_LocateXboxBackup_Click(object sender, EventArgs e)
        {
            ShowFile(xboxFilePathBackup, xbox, true);
        }

        private void btn_Minimize_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void btn_Close_Click(object sender, EventArgs e)
        {
            Close();
        }

        //Change colour of Close and Minimize buttons when mouse enters/exits.
        private void btn_Minimize_MouseEnter(object sender, EventArgs e)
        {
            btn_Minimize.ForeColor = Color.Black;
            btn_Minimize.BackColor = Color.Orange;
        }

        private void btn_Minimize_MouseLeave(object sender, EventArgs e)
        {
            btn_Minimize.ForeColor = Color.Orange;
            btn_Minimize.BackColor = Color.Black;
        }
        private void btn_Close_MouseEnter(object sender, EventArgs e)
        {
            btn_Close.ForeColor = Color.Black;
            btn_Close.BackColor = Color.Orange;
        }

        private void btn_Close_MouseLeave(object sender, EventArgs e)
        {
            btn_Close.ForeColor = Color.Orange;
            btn_Close.BackColor = Color.Black;
        }

        #endregion Buttons

        #region Menu-Strip Items

        private void setBackupFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetFolder(ref backupFolderPath, true, backup);
            LocateFile(steam, true);
            LocateFile(xbox, true);
        }

        private void resetDefaultBackupFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = "Reset backup folder location to default?" +
               "\n\nThis will only change the folder path, " +
               "\nfiles will not be affected." +
               "\n\nAre you sure? ";

            CustomMessageBox("Confirmation", message, true, null);
            if (dialogResultCustom == DialogResult.Yes)
            {
                backupFolderPath = backupFolderPathDefault;
                SaveSettings();
                LocateFile(steam, true);
                LocateFile(xbox, true);
            }
        }

        private void chooseSteamSaveFolderToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SetFolder(ref steamFolderPath, false, steam);
            LocateFile(steam, false);
        }

        private void resetSteamFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = "Reset Steam save folder location to default?" +
               "\n\nThis will only change the folder path, " +
               "\nsave file will not be affected." +
               "\n\nAre you sure? ";

            CustomMessageBox("Confirmation", message, true, null);
            if (dialogResultCustom == DialogResult.Yes)
            {
                steamFolderPath = steamFolderPathDefault;
                LocateFile(steam, false);
                SaveSettings();
            }
        }

        private void chooseXboxFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetFolder(ref xboxFolderPath, false, xbox);
            LocateFile(steam, false);
        }

        private void resetXboxFolderToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string message = "Reset Xbox save folder location to default?" +
               "\n\nThis will only change the folder path, " +
               "\nsave file will not be affected." +
               "\n\nAre you sure? ";

            CustomMessageBox("Confirmation", message, true, null);
            if (dialogResultCustom == DialogResult.Yes)
            {
                xboxFolderPath = xboxFolderPathDefault;
                LocateFile(xbox, false);
                SaveSettings();
            }
        }

        private void resetAllDefaultFoldersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = "Reset all folder locations to default?" +
                "\n\nThis will only change the folder paths, saves will not be affected." +
                "\n\nAre you sure? ";

            CustomMessageBox("Confirmation", message, true, null);
            if (dialogResultCustom == DialogResult.Yes)
            {
                backupFolderPath = backupFolderPathDefault;
                LocateFile(steam, true);
                LocateFile(xbox, true);

                steamFolderPath = steamFolderPathDefault;
                LocateFile(steam, false);

                xboxFolderPath = xboxFolderPathDefault;
                LocateFile(xbox, false);

                SaveSettings();
            }
        }

        private void detectFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LocateFile(steam, false);
            LocateFile(xbox, false);
            LocateFile(steam, true);
            LocateFile(xbox, true);

            label_StatusInfo.Text = "Refreshed File Detection.";
        }

        private void createBackupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = "Create new backups?";

            CustomMessageBox("Confirmation", message, true, null);
            if (dialogResultCustom == DialogResult.Yes)
            {
                CreateBackups();
                label_StatusInfo.Text = backupSuccessfulMessage;
            }
        }

        private void restoreSteamToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = "Steam save will be overwritten by backup." +
                "\n\nAny game progress since last backup will be lost." +
                "\n\nAre you sure?";

            CustomMessageBox("Confirmation", message, true, null);
            if (dialogResultCustom == DialogResult.Yes)
            {
                RestoreBackups(true, false);
            }
        }

        private void restoreXboxToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = "Xbox save will be overwritten by backup." +
                "\n\nAny game progress since last backup will be lost." +
                "\n\nAre you sure?";

            CustomMessageBox("Confirmation", message, true, null);
            if (dialogResultCustom == DialogResult.Yes)
            {
                RestoreBackups(false, true);
            }
        }

        private void restoreAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = "Both Steam and Xbox saves will be overwritten by backups." +
                "\n\nAny game progress since last backup will be lost." +
                "\n\nAre you sure?";

            CustomMessageBox("Confirmation", message, true, null);
            if (dialogResultCustom == DialogResult.Yes)
            {
                RestoreBackups(true, true);
            }
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = "When selecting \"Sync Most Progress\", the program " +
                "\nwill automatically determine which save file has the most progress." +
                "\n\nIt will consider two things; " +
                "\nfile size and most recent file write." +
                "\n\nLarger file size is a better indicator of" +
                "\nfurther game progress than most recent file write." +
                "\n\nIf the difference in file size is above a certain amount," +
                "\nThe larger file will be chosen." +
                "\n\n If file sizes are similar, " +
                "\nthe file with the most recent changes will be chosen." +
                "\n\nIf the program's decision doesn't seem right, " +
                "\nopen each save file in-game and verify which has the most progress.";

            CustomMessageBox("How Does It Work?", message, false, null);
        }

        #endregion Menu-Strip Items

        #endregion Controls

    }
}
