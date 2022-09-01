/////////////////////////////////////////////////////////////////////////////////////////
//53B19D17B86453DDBE3E8F24C52E96A9
//09B7BAA5086E3F2FE3E2C537FC49DB6D38074626
//Author: Adam Kim
//Created On: 8/25/2020
//Last Modified On: 11/5/2020
//Copyright: USAF // JT4 LLC
//Description: Main window of the PodLoad application
/////////////////////////////////////////////////////////////////////////////////////////
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.Controls;
using Serilog;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using System.Xml;
using System;

namespace PodLoad_WPF
{
    /////////////////////////////////////////////////////////////////////////////////////////
    // Currently the application is in a mostly stable state. I just wanted to note that there
    // are commented out ASYNC functions scattered about. They are to draw the dialog box
    // on top of the existing UI in a seperate thread. Unfortunately, I'm not great with
    // multi-threaded programming so I could not get it to function properly without race
    // conditions and deadlocks. Visually they are much nicer so I'd recommend using them
    // if they can be properly implemented. I tried to keep the rest of the code as minimal
    // and efficient as possible, but I'm still very much a beginner so it's definitely not
    // perfect and can surely do with some optimizations and improvements. I hope I can
    // atleast provide a solid base to build off of. -Adam
    /////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Class for generating a user input dialog box. Specifically sized for prompting for an admin password, but can be used for any user input.
    /// </summary>
    public static class Prompt
    {
        /// <summary>
        /// Function to present a dialog prompt where a user can input a value which is returned.
        /// </summary>
        /// <param name="text">The text found in the body of the dialog box</param>
        /// <param name="caption">The caption found at the top of the dialog box</param>
        /// <returns>A string of the user inputted value</returns>
        public static string ShowDialog(string text, string caption)
        {
            System.Windows.Forms.Form prompt = new System.Windows.Forms.Form()
            {
                Width = 430,
                Height = 155,
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
            };

            System.Windows.Forms.Label textLabel = new System.Windows.Forms.Label() { Left = 50, Top = 15, Width = 330, Height = 30, Text = text };
            System.Windows.Forms.TextBox textBox = new System.Windows.Forms.TextBox() { Left = 50, Top = 50, Width = 315 };
            System.Windows.Forms.Button confirmation = new System.Windows.Forms.Button() { Text = "OK", Left = 315, Width = 50, Top = 80, DialogResult = System.Windows.Forms.DialogResult.OK };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == System.Windows.Forms.DialogResult.OK ? textBox.Text : "";
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        /// <summary>
        /// Create global variables for logging, data table, app version, temp files, current color mode, a temp flag for making changes, a temp flag for making edits, an empty temp GUID var, and a temp var for 
        /// the deleted row count.
        /// Since file paths are hardcoded they will need to be changed here and in SyncButton_Click whenever a change needs to be made.
        /// </summary>
        DataTable dt = new DataTable();
        ILogger podloadLog = new LoggerConfiguration().WriteTo.File(Directory.GetParent(Assembly.GetExecutingAssembly().Location) + "/Data/Archive/Log.txt").CreateLogger();
        string appVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title + Assembly.GetExecutingAssembly().GetName().Version;
        string fileName = Directory.GetParent(Assembly.GetExecutingAssembly().Location) + "/Data/Temp/Temp.xml";
        string colorMode = ConfigurationManager.AppSettings.Get("colorMode");
        bool debugMode = bool.Parse(ConfigurationManager.AppSettings.Get("debugMode"));
        bool changeFlag = false;
        bool editFlag = false;
        Guid tempGUID = Guid.Empty;
        int deletedRows = 0;

        /// <summary>
        /// Initial function to initialize the form, saves the passed log into the global and creates a new XML file for temp storage of values.
        /// </summary>
        /// <returns>None</returns>
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                CreateFile(true);
                DarkLightToggle(true);

                // Check if in debug mode.
                if (debugMode)
                {
                    //this.ShowMessageAsync("ALERT", "YOU ARE CURRENTLY IN DEBUG MODE. If this is incorrectly configured edit the 'debugMode' value in 'PodLoad_WPF.dll.config'.");
                    MessageBox.Show("YOU ARE CURRENTLY IN DEBUG MODE. If this is incorrectly configured edit the 'debugMode' value in 'PodLoad_WPF.dll.config'.", "ALERT");
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'MainWindow' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'MainWindow' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to return the selected mode (upload, download or change).
        /// </summary>
        /// <param name="change">A flag for tracking whether the selected action is a change from the second window. Default is false</param>
        /// <returns>A string with the given action</returns>
        private string ActionValue(bool change)
        {
            try
            {
                // If performing a change, return change specific status codes.
                if (change)
                {
                    if ((bool)downloadingButton.IsChecked)
                    {
                        return "CHANGE_DOWNLOAD";
                    }
                    else
                    {
                        return "CHANGE_UPLOAD";
                    }
                }
                else if ((bool)downloadingButton.IsChecked)
                {
                    return "DOWNLOAD";
                }
                else
                {
                    return "UPLOAD";
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ActionValue' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ActionValue' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return null;
            }
        }

        /// <summary>
        /// Function that recieves a user's input via InputBox to check if they should have admin access.
        /// </summary>
        /// <returns>A boolean if the user should have admin access or not</returns>
        private bool AdminAccess()
        {
            try
            {
                //var userInput = this.ShowInputAsync("ALERT", "Please enter the admin password to get access to this tool.");
                string userInput = Prompt.ShowDialog("Please enter the admin password to get access to this tool.", "ADMIN AUTHENICATION");

                // Check against password stored in the App.config file.
                if (userInput == ConfigurationManager.AppSettings.Get("adminPass"))
                {
                    return true;
                }
                else
                {
                    //this.ShowMessageAsync("ACCESS DENIED", "Sorry that password was incorrect. Please try again or contact your local administrator.");
                    MessageBox.Show("Sorry that password was incorrect. Please try again or contact your local administrator.", "ACCESS DENIED");
                    return false;
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'AdminAccess' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'AdminAccess' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return false;
            }
        }

        /// <summary>
        /// Function to search by DRDNO, PodNO, and Unit between a range of dates. Opens a new window.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void ChangeButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Check for the admin password before continuing.
                if (AdminAccess())
                {
                    // Check if in debug mode.
                    if (debugMode)
                    {
                        //this.ShowMessageAsync("ALERT", "YOU ARE CURRENTLY IN DEBUG MODE. This window does not function without a connection to the database, if you attempt to perform a search the application" +
                        //" will hang (freeze) for 10 seconds as it tries to establish a connection. Please be patient, if it doesn't return to normal functionality restart the application.");
                        MessageBox.Show("YOU ARE CURRENTLY IN DEBUG MODE. This window does not function without a connection to the database, if you attempt to perform a search the application" +
                            " will hang (freeze) for 10 seconds as it tries to establish a connection. Please be patient, if it doesn't return to normal functionality restart the application.", "ALERT");
                    }

                    ChangeWindow changeWindow = new ChangeWindow(podloadLog, colorMode);
                    changeWindow.Owner = this;
                    changeWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ChangeButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ChangeButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to change the available condition code options in the drop down menu. Changes values if a change is being made.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void ChangeConditionCodeOptions(bool change)
        {
            try
            {
                codeInput.Items.Clear();

                // Check what mode we are currently in and add standard options if not performing a change.
                if ((bool)uploadingButton.IsChecked && !change)
                {
                    codeInput.Items.Add("UPLD - Standard Upload");
                }
                else if ((bool)downloadingButton.IsChecked && !change)
                {
                    codeInput.Items.Add("DNLD - Standard Download");
                }

                // Check if making a change, if yes add change specific codes.
                if (change)
                {
                    codeInput.Items.Add("CHNG - Record Correction");
                    codeInput.Items.Add("CRNG - Change Exercise");
                    codeInput.Items.Add("CSTN - Change Station");
                }

                // Always added regardless of condition.
                codeInput.Items.Add("CLHH - Location: Hush House");
                codeInput.Items.Add("CLLB - Location: Loading Barn");
                codeInput.Items.Add("CPMI - Preventative Maintenance Inspection");
                codeInput.Items.Add("CRTB - Return To Base");
                codeInput.Items.Add("CWLT - Weapons Load Training");
                codeInput.Items.Add("DNTO - Did Not Turn On");
                codeInput.Items.Add("LOLA - Location: Live Ordinance Loading Area");
                codeInput.Items.Add("RCAC - Reconfigure Aircraft");
                codeInput.Items.Add("TCTO - Time Compliance Technical Order");
                codeInput.Items.Add("TRVL - Aircraft Needs To Travel");
                codeInput.Items.Add("OTHR - Other (Comment Required)");
                codeInput.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ChangeConditionCodeOptions' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ChangeConditionCodeOptions' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to accept the values selected in the change window, then it populates the respective fields to perform a change.
        /// </summary>
        /// <param name="GUID">The guid of the record to change</param>
        /// <param name="workShift">The work shift of the record to change</param>
        /// <param name="unit">The unit of the record to change</param>
        /// <param name="TDY">The TDY option of the record to change</param>
        /// <param name="aircraftType">The aircraft type of the record to change</param>
        /// <param name="tailNO">The tail no of the record to change</param>
        /// <param name="aircraftStatus">The aircraft status of the record to change</param>
        /// <param name="podNO">The pod no of the record to change</param>
        /// <param name="weighted">The weighted option of the record to change</param>
        /// <param name="DRDNO">The DRD no of the record to change</param>
        /// <param name="torqueWrench">The torque wrench of the record to change</param>
        /// <param name="station">The station of the record to change</param>
        /// <param name="exercise">The exercise of the record to change</param>
        /// <param name="conditionCode">The condition code of the record to change</param>
        /// <param name="comment">The comment of the record to change</param>
        /// <param name="tracking">The tracking option of the record to change</param>
        /// <param name="mode">The mode of the record to change</param>
        /// <returns>None (Void)</returns>
        public void ChangeModeMoveResults(Guid GUID, string workShift, string unit, bool TDY, string aircraftType, string tailNO, string aircraftStatus, string podNO, bool weighted, string DRDNO, 
            string torqueWrench, string station, string exercise, string conditionCode, string tracking, string comment, string mode)
        {
            try
            {
                // Set which mode button is checked.
                if (mode == "UPLOAD" || mode == "SWAP")
                {
                    uploadingButton.IsChecked = true;
                    downloadingButton.IsChecked = false;
                }
                else if (mode == "DOWNLOAD")
                {
                    uploadingButton.IsChecked = false;
                    downloadingButton.IsChecked = true;
                }

                // Enable/Disable input based on mode button and prep work shift buttons and status buttons by unchecking all of them.
                // Then assign values to their respective fields.
                EnableDisableInputs();
                shift1Button.IsChecked = false;
                shift2Button.IsChecked = false;
                shift3Button.IsChecked = false;
                shift4Button.IsChecked = false;
                ShiftValue(workShift);
                unitInput.Text = unit;
                tdyBox.IsChecked = TDY;
                aircraftInput.Text = aircraftType;
                tailnoInput.Text = tailNO;
                statusmreqButton.IsChecked = false;
                statussprButton.IsChecked = false;
                StatusValue(aircraftStatus);
                podnoInput.Text = podNO;
                weightedBox.IsChecked = weighted;
                drdnoInput.Text = DRDNO;
                torqueInput.Text = torqueWrench;
                stationInput.Text = station;
                exerciseInput.Text = exercise;
                TrackingValue(tracking);

                // Check if comment has already been modified.
                if (comment.Contains("(Original Comment)"))
                {
                    commentInput.Text = comment;
                }
                else
                {
                    commentInput.Text = comment + " (Original Comment)";
                }

                tempGUID = GUID;

                // Set changeFlag to denote that a change is being made.
                changeFlag = true;
                ChangeConditionCodeOptions(changeFlag);

                // Since code is returned as a 4 letter code, check which option it matches and select it in the list.
                foreach (var cbi in codeInput.Items)
                {
                    if (cbi.ToString().Contains(conditionCode))
                    {
                        codeInput.SelectedItem = cbi;
                    }
                }

                // Check to see if performing a swap, if yes then disable all fields except for Workshift, DRDNO, and comments.
                if (mode == "SWAP")
                {
                    PerformSwap(true);
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ChangeModeMoveResults' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ChangeModeMoveResults' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to change the available station options (and weighted option) in the drop down menu. Changes values based on the selected input in the aircraftInput text field.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void ChangeStationOptions(object sender, EventArgs e)
        {
            try
            {
                stationInput.Items.Clear();

                if (aircraftInput.Text == "A-10")
                {
                    stationInput.Items.Add("1I");
                    stationInput.Items.Add("1O");
                    stationInput.Items.Add("11I");
                    stationInput.Items.Add("11O");   
                }
                else if (aircraftInput.Text == "F-15C" || aircraftInput.Text == "F-15D" || aircraftInput.Text == "F-15E")
                {
                    stationInput.Items.Add("2A");
                    stationInput.Items.Add("2B");
                    stationInput.Items.Add("8A");
                    stationInput.Items.Add("8B");
                }
                else if (aircraftInput.Text == "F-16")
                {
                    stationInput.Items.Add("1");
                    stationInput.Items.Add("2");
                    stationInput.Items.Add("8");
                    stationInput.Items.Add("9");
                }
                else if (aircraftInput.Text == "F-18A" || aircraftInput.Text == "F-18B" || aircraftInput.Text == "F-18C" || aircraftInput.Text == "F-18D")
                {
                    stationInput.Items.Add("1");
                    stationInput.Items.Add("2");
                    stationInput.Items.Add("9");
                    stationInput.Items.Add("10");
                }
                else if (aircraftInput.Text == "F-18E" || aircraftInput.Text == "F-18F" || aircraftInput.Text == "F-18G")
                {
                    stationInput.Items.Add("1");
                    stationInput.Items.Add("2");
                    stationInput.Items.Add("10");
                    stationInput.Items.Add("11");
                }
                else if (aircraftInput.Text == "L-159" || aircraftInput.Text == "TYPHOON" || aircraftInput.Text == "B-52")
                {
                    stationInput.Items.Add("R");
                    stationInput.Items.Add("L");
                }
                else
                {
                    stationInput.Items.Add("1");
                    stationInput.Items.Add("2");
                    stationInput.Items.Add("3");
                    stationInput.Items.Add("4");
                    stationInput.Items.Add("5");
                    stationInput.Items.Add("6");
                    stationInput.Items.Add("7");
                    stationInput.Items.Add("8");
                    stationInput.Items.Add("9");
                }

                // Also include a check for the weighted box.
                if (aircraftInput.Text == "F-18A" || aircraftInput.Text == "F-18B" || aircraftInput.Text == "F-18C" || aircraftInput.Text == "F-18D" || aircraftInput.Text == "F-18E" || aircraftInput.Text == "F-18F" ||
                    aircraftInput.Text == "F-18G" || aircraftInput.Text == "TYPHOON")
                {
                    weightedBox.IsChecked = true;
                    weightedBox.IsEnabled = true;
                }
                else
                {
                    weightedBox.IsChecked = false;
                    weightedBox.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ChangeStationOptions' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ChangeStationOptions' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to change the available tailno options in the drop down menu. Changes values based on the selected input in the unitInput text field.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void ChangeTailNOOptions()
        {
            try
            {
                tailnoInput.Items.Clear();

                if (unitInput.Text == "VIPER TFA")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("0220");
                    tailnoInput.Items.Add("1159");
                    tailnoInput.Items.Add("1220");
                    tailnoInput.Items.Add("1236");
                    tailnoInput.Items.Add("1244");
                    tailnoInput.Items.Add("1301");
                    tailnoInput.Items.Add("1418");
                    tailnoInput.Items.Add("2048");
                    tailnoInput.Items.Add("251");
                    tailnoInput.Items.Add("267");
                    tailnoInput.Items.Add("271");
                    tailnoInput.Items.Add("272");
                    tailnoInput.Items.Add("273");
                    tailnoInput.Items.Add("280");
                    tailnoInput.Items.Add("283");
                    tailnoInput.Items.Add("291");
                    tailnoInput.Items.Add("299");
                    tailnoInput.Items.Add("307");
                    tailnoInput.Items.Add("313");
                    tailnoInput.Items.Add("321");
                    tailnoInput.Items.Add("707");
                    tailnoInput.Items.Add("750");
                }
                else if (unitInput.Text == "VIPER TF")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("475");
                    tailnoInput.Items.Add("728");
                    tailnoInput.Items.Add("729");
                    tailnoInput.Items.Add("739");
                    tailnoInput.Items.Add("746");
                    tailnoInput.Items.Add("747");
                    tailnoInput.Items.Add("839");
                    tailnoInput.Items.Add("926");
                }
                else if (unitInput.Text == "TOMAHAWK")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("174");
                    tailnoInput.Items.Add("2015");
                    tailnoInput.Items.Add("2067");
                    tailnoInput.Items.Add("2072");
                    tailnoInput.Items.Add("2075");
                    tailnoInput.Items.Add("2081");
                    tailnoInput.Items.Add("2083");
                    tailnoInput.Items.Add("2090");
                    tailnoInput.Items.Add("2092");
                    tailnoInput.Items.Add("2116");
                    tailnoInput.Items.Add("2119");
                    tailnoInput.Items.Add("2149");
                    tailnoInput.Items.Add("2176");
                    tailnoInput.Items.Add("416");
                    tailnoInput.Items.Add("436");
                    tailnoInput.Items.Add("439");
                    tailnoInput.Items.Add("447");
                    tailnoInput.Items.Add("486");
                    tailnoInput.Items.Add("495");
                    tailnoInput.Items.Add("503");
                    tailnoInput.Items.Add("533");
                    tailnoInput.Items.Add("744");
                }
                else if (unitInput.Text == "THUNDER CB")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("169");
                    tailnoInput.Items.Add("171");
                    tailnoInput.Items.Add("199");
                    tailnoInput.Items.Add("242");
                    tailnoInput.Items.Add("658");
                }
                else if (unitInput.Text == "THUNDER TF")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("176");
                    tailnoInput.Items.Add("185");
                    tailnoInput.Items.Add("186");
                    tailnoInput.Items.Add("1991");
                    tailnoInput.Items.Add("1992");
                    tailnoInput.Items.Add("665");
                    tailnoInput.Items.Add("671");
                    tailnoInput.Items.Add("709");
                    tailnoInput.Items.Add("8204");
                }
                else if (unitInput.Text == "STRIKE CB")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("1325");
                    tailnoInput.Items.Add("1601");
                    tailnoInput.Items.Add("258");
                    tailnoInput.Items.Add("322");
                    tailnoInput.Items.Add("365");
                    tailnoInput.Items.Add("6200");
                    tailnoInput.Items.Add("7217");
                }
                else if (unitInput.Text == "STRIKE TF")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("1305");
                    tailnoInput.Items.Add("1328");
                    tailnoInput.Items.Add("239");
                    tailnoInput.Items.Add("256");
                    tailnoInput.Items.Add("257");
                    tailnoInput.Items.Add("260");
                    tailnoInput.Items.Add("261");
                    tailnoInput.Items.Add("262");
                    tailnoInput.Items.Add("9251");
                }
                else if (unitInput.Text == "EAGLE CB")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("1030");
                    tailnoInput.Items.Add("1063");
                    tailnoInput.Items.Add("3037");
                    tailnoInput.Items.Add("3040");
                }
                else if (unitInput.Text == "EAGLE TF")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("1033");
                    tailnoInput.Items.Add("2018");
                    tailnoInput.Items.Add("2022");
                    tailnoInput.Items.Add("3014");
                    tailnoInput.Items.Add("3019");
                    tailnoInput.Items.Add("3026");
                    tailnoInput.Items.Add("3050");
                    tailnoInput.Items.Add("4024");
                    tailnoInput.Items.Add("4045");
                    tailnoInput.Items.Add("8327");
                    tailnoInput.Items.Add("8484");
                    tailnoInput.Items.Add("8517");
                }
                else if (unitInput.Text == "FALCON")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("326");
                    tailnoInput.Items.Add("374");
                    tailnoInput.Items.Add("404");
                    tailnoInput.Items.Add("420");
                    tailnoInput.Items.Add("423");
                    tailnoInput.Items.Add("442");
                    tailnoInput.Items.Add("499");
                    tailnoInput.Items.Add("721");
                    tailnoInput.Items.Add("726");
                    tailnoInput.Items.Add("740");
                    tailnoInput.Items.Add("757");
                    tailnoInput.Items.Add("809");
                }
                else if (unitInput.Text == "DRAKEN")
                {
                    tailnoInput.IsEditable = false;
                    tailnoInput.Items.Add("159");
                    tailnoInput.Items.Add("256");
                    tailnoInput.Items.Add("257");
                    tailnoInput.Items.Add("258");
                    tailnoInput.Items.Add("259");
                    tailnoInput.Items.Add("262");
                    tailnoInput.Items.Add("263");
                    tailnoInput.Items.Add("264");
                    tailnoInput.Items.Add("265");
                    tailnoInput.Items.Add("266");
                    tailnoInput.Items.Add("267");
                    tailnoInput.Items.Add("269");
                    tailnoInput.Items.Add("270");
                    tailnoInput.Items.Add("271");
                    tailnoInput.Items.Add("273");
                    tailnoInput.Items.Add("274");
                    tailnoInput.Items.Add("275");
                    tailnoInput.Items.Add("277");
                }
                else
                {
                    tailnoInput.IsEditable = true;
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ChangeTailNOOptions' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ChangeTailNOOptions' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to check if inputed value is a integer or not. If not do not accept value.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any text composition event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void CheckIfInt(object sender, TextCompositionEventArgs e)
        {
            try
            {
                Regex tempRegex = new Regex("[^0-9]+");
                e.Handled = tempRegex.IsMatch(e.Text);
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'CheckIfInt' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'CheckIfInt' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to clear the text boxes on the form and input the default values depending on the data entry mode it is currently in (upload/download).
        /// </summary>
        /// <returns>None (Void)</returns>
        private void ClearSelections()
        {
            try
            {
                // Add N/A value to torque wrench if doing a download, otherwise leave empty.
                if ((bool)uploadingButton.IsChecked)
                {
                    torqueInput.Text = String.Empty;
                    drdnoInput.Text = String.Empty;
                }
                else
                {
                    torqueInput.Text = "N/A";
                    drdnoInput.Text = "N/A";
                }

                // Clear all fields with an empty string.
                unitInput.Text = String.Empty;
                aircraftInput.Text = String.Empty;
                tailnoInput.Text = String.Empty;
                podnoInput.Text = String.Empty;
                stationInput.Text = String.Empty;
                exerciseInput.SelectedIndex = -1;
                codeInput.SelectedIndex = -1;
                commentInput.Text = String.Empty;
                tdyBox.IsChecked = false;
                statusmreqButton.IsChecked = true;
                statussprButton.IsChecked = false;
                weightedBox.IsChecked = false;
                trackingBTBox.IsChecked = false;
                trackingNTBox.IsChecked = false;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ClearSelections' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ClearSelections' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function for button click to toggle the code key grid.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void CodeKeyToggleButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Since I couldn't figure out how to dynamically resize the elements using the built in WPF stackpanels/dockpanels I have to manually resize the necessary elements
                // to get everything to fit and scale correctly.
                if (codeKeyGrid.Visibility == Visibility.Collapsed)
                {
                    codeKeyGrid.Visibility = Visibility.Visible;
                    saveButton.Margin = new Thickness(512, 0, 0, 180);
                    syncButton.Margin = new Thickness(0, 0, 22, 180);
                    commentInput.Margin = new Thickness(26, 536, 0, 180);
                    displayedTable.Margin = new Thickness(513, 107, 22, 240);
                }
                else
                {
                    codeKeyGrid.Visibility = Visibility.Collapsed;
                    saveButton.Margin = new Thickness(512, 0, 0, 40);
                    syncButton.Margin = new Thickness(0, 0, 22, 40);
                    commentInput.Margin = new Thickness(26, 536, 0, 40);
                    displayedTable.Margin = new Thickness(513, 107, 22, 120);
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'CodeKeyToggleButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'CodeKeyToggleButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to auto open the combobox dropdown menu when the element receives focus.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any keyboard focus changed event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void ComboBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            try
            {
                ComboBox comboBox = sender as ComboBox;
                comboBox.IsDropDownOpen = true;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ComboBox_GotKeyboardFocus' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ComboBox_GotKeyboardFocus' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to limit the input values in a combobox to 50 chars (Same limit imposed by the fields in the DB).
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any routed event args sent by sender</param>
        /// <returns>None (Void)</returns>
        public void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var sentOBJ = (ComboBox)sender;

                if (sentOBJ != null)
                {
                    var sentTextBox = (TextBox)sentOBJ.Template.FindName("PART_EditableTextBox", sentOBJ);

                    if (sentTextBox != null)
                    {
                        sentTextBox.MaxLength = 50;
                    }
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ComboBox_Loaded' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ComboBox_Loaded' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to create the temp XML file for storing inputed values. If the file exists it uses that to populate the dataviewgrid, if not then create a new file and generate the schema.
        /// </summary>
        /// <param name="start">Flag to check if program is just starting up. True is sent when program first starts, otherwise false is sent</param>
        /// <returns>None (Void)</returns>
        private void CreateFile(bool start)
        {
            try
            {
                // If file already exists then open and read file.
                if (File.Exists(fileName))
                {
                    dt.ReadXml(fileName);
                    displayedTable.ItemsSource = dt.DefaultView;
                }
                // If start of program then generate file (if does not already exist).
                else if (start)
                {
                    Directory.CreateDirectory(Directory.GetParent(Assembly.GetExecutingAssembly().Location) + "/Data/Temp/");
                    dt.TableName = "PodFormTable";
                    dt.Columns.Add("Done", typeof(bool));
                    dt.Columns.Add("WorkShift", typeof(string));
                    dt.Columns.Add("Unit", typeof(string));
                    dt.Columns.Add("TDY", typeof(bool));
                    dt.Columns.Add("AircraftType", typeof(string));
                    dt.Columns.Add("TailNO", typeof(string));
                    dt.Columns.Add("AircraftStatus", typeof(string));
                    dt.Columns.Add("PodNO", typeof(string));
                    dt.Columns.Add("Weighted", typeof(bool));
                    dt.Columns.Add("DRDNO", typeof(string));
                    dt.Columns.Add("TorqueWrench", typeof(string));
                    dt.Columns.Add("Station", typeof(string));
                    dt.Columns.Add("Exercise", typeof(string));
                    dt.Columns.Add("ConditionCode", typeof(string));
                    dt.Columns.Add("Tracking", typeof(string));
                    dt.Columns.Add("Comments", typeof(string));
                    dt.Columns.Add("Timestamp", typeof(DateTime));
                    dt.Columns.Add("ActionType", typeof(string));
                    dt.Columns.Add("UpdateGUID", typeof(Guid));
                    dt.WriteXml(fileName, XmlWriteMode.WriteSchema);
                }
                // If table needs to be cleared (Synced with DB) then generate new file from existing schema.
                else
                {
                    dt.Clear();
                    dt.WriteXml(fileName, XmlWriteMode.WriteSchema);
                }

                // Update the row count in the status bar.
                deletedRows = 0;
                UpdateRowCount();

                // Reset all of the done boxes since the application has been closed/crashed/reset.
                DataGrid_DoneReset();
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'CreateFile' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'CreateFile' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to change the mode to light or dark mode depending on what is toggled. Broken out from button click. Defaults to dark mode.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void DarkLightToggle(bool start)
        {
            try
            {
                ResourceDictionary mainTheme = null;
                ResourceDictionary materialTheme = null;
                Brush menuColor = null;

                if ((colorMode == "LIGHT" && start) || (colorMode == "DARK" && !start))
                {
                    mainTheme = new ResourceDictionary() { Source = new Uri("pack://application:,,,/MahApps.Metro;component/Styles/Themes/Light.Steel.xaml", UriKind.Absolute) };
                    materialTheme = new ResourceDictionary() { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml", UriKind.Absolute) };
                    menuColor = (Brush)new BrushConverter().ConvertFromString("#FF83919f");
                    colorMode = "LIGHT";
                }
                else if ((colorMode == "DARK" && start) || (colorMode == "LIGHT" && !start))
                {
                    mainTheme = new ResourceDictionary() { Source = new Uri("pack://application:,,,/MahApps.Metro;component/Styles/Themes/Dark.Steel.xaml", UriKind.Absolute) };
                    materialTheme = new ResourceDictionary() { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml", UriKind.Absolute) };
                    menuColor = (Brush)new BrushConverter().ConvertFromString("#FF576573");
                    colorMode = "DARK";
                }

                // Change resource dictionaries/keys for both the main theme and the material theme.
                foreach (var mergeDict in mainTheme.MergedDictionaries)
                {
                    App.Current.Resources.MergedDictionaries.Add(mergeDict);
                }
                foreach (var key in mainTheme.Keys)
                {
                    App.Current.Resources[key] = mainTheme[key];
                }
                foreach (var mergeDict in materialTheme.MergedDictionaries)
                {
                    App.Current.Resources.MergedDictionaries.Add(mergeDict);
                }
                foreach (var key in materialTheme.Keys)
                {
                    App.Current.Resources[key] = materialTheme[key];
                }

                // Change the menu color background dependent on the current mode.
                mainMenu.Background = menuColor;
                mainMenuButton.Background = menuColor;
                dashboardMenuButton.Background = menuColor;
                swapMenuButton.Background = menuColor;
                changeMenuButton.Background = menuColor;
                exportMenuButton.Background = menuColor;
                phoneMenuButton.Background = menuColor;
                toggleMenuButton.Background = menuColor;
                toggleCodeKeyButton.Background = menuColor;
                helpMenuButton.Background = menuColor;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DarkLightToggle' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DarkLightToggle' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to check if the datagrid is currently being edited. Added to avoid a bug when a cell is selected and the delete key is pressed.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any datagrid beginning edit event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            try
            {
                editFlag = true;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataGrid_BeginningEdit' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataGrid_BeginningEdit' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to check if the datagrid is done being edited. Added to avoid a bug when a cell is selected and the delete key is pressed.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any datagrid edit ending event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                // Write to XML file any changes that were made in the cell, then change edit flag to reflect leaving edit mode.
                dt.WriteXml(fileName, XmlWriteMode.WriteSchema);
                editFlag = false;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataGrid_CellEditEnding' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataGrid_CellEditEnding' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to delete the selected row in the datagridview and also deletes the respective node in the XML file.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any keyboard event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void DataGrid_Delete(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Delete && (bool)editBox.IsChecked && !editFlag)
                {
                    // Delete the correct node from the XML file.
                    XML_Delete();

                    // Delete the correct row from the datatable.
                    dt.Rows[displayedTable.SelectedIndex].Delete();

                    // Refresh the datagrid with the new datatable.
                    displayedTable.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataGrid_Delete' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataGrid_Delete' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary> 
        /// Function to clear the current selection in the datagridview when the datagrid loses focus.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any mouse button event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void DataGrid_Deselect(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DataGridRow row = displayedTable.ItemContainerGenerator.ContainerFromItem(displayedTable.SelectedItem) as DataGridRow;

                // Check if the selected row is null or if the mouse if over it (in edit mode).
                if (row != null && !row.IsMouseOver)
                {
                    displayedTable.SelectedItem = null;
                    displayedTable.CommitEdit();
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataGrid_Deselect' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataGrid_Deselect' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to check if all of the "Done" boxes are checked, if not then do not submit results until box is checked or row is deleted.
        /// </summary>
        /// <returns>Returns a bool based on if all "Done" boxes are checked. Only returns null when exception is caught</returns>
        private bool? DataGrid_Done()
        {
            try
            {
                // Check each row to see if the "Done" box has been checked or if the checkbox shows up as null.
                foreach (DataRowView row in displayedTable.Items)
                {
                    var tempCB = displayedTable.Columns[0].GetCellContent(row) as CheckBox;

                    if (tempCB == null || !(bool)tempCB.IsChecked)
                    {
                        //this.ShowMessageAsync("ALERT", "The 'Done' box needs to be checked on every row to sync. Either check the box on each row or delete the row to continue.");
                        MessageBox.Show("The 'Done' box needs to be checked on every row to sync. Either check the box on each row or delete the row to continue.", "ALERT");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataGrid_Done' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataGrid_Done' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return null;
            }
        }

        /// <summary>
        /// Function to reset all of the "Done" checkboxes.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void DataGrid_DoneReset()
        {
            try
            {
                foreach (DataRow row in dt.Rows)
                {
                    row["Done"] = false;
                }

                // Not necessary, but still write the changes to the XML file for later reference.
                dt.WriteXml(fileName, XmlWriteMode.WriteSchema);
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataGrid_DoneReset' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataGrid_DoneReset' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to all user to edit data in datagridview if check box is checked. If unchecked then table returns to "read-only" mode. First column always needs to not be in "read-only" mode.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void DataGrid_Edit(object sender, EventArgs e)
        {
            try
            {
                // Set all columns to either read only or non-read only mode.
                foreach (DataGridColumn column in displayedTable.Columns)
                {
                    if ((bool)editBox.IsChecked)
                    {
                        displayedTable.CanUserDeleteRows = true;
                        column.IsReadOnly = false;
                    }
                    else
                    {
                        displayedTable.CanUserDeleteRows = false;
                        column.IsReadOnly = true;
                    }
                }

                // First column should always be editable.
                displayedTable.Columns[0].IsReadOnly = false;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataGrid_Edit' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataGrid_Edit' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to highlight the row that is currently being edited.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any text changed event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void DataGrid_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                TextBox textBox = sender as TextBox;
                DataGridRow datagridRow = DataGridRow.GetRowContainingElement(textBox);
                displayedTable.SelectedIndex = datagridRow.GetIndex();
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataGrid_TextChanged' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataGrid_TextChanged' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to call the XML_Delete function whenever the "Delete Row" option is selected in the datagrid context menu.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any routed event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void DataGridContext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((bool)editBox.IsChecked && !editFlag && displayedTable.SelectedItem != null)
                {
                    // Delete the correct node from the XML file.
                    XML_Delete();

                    // Delete the correct row from the datatable.
                    dt.Rows[displayedTable.SelectedIndex].Delete();

                    // Refresh the datagrid with the new datatable.
                    displayedTable.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataGridContext_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataGridContext_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function that returns the respective empty fields.
        /// </summary>
        /// <returns>String of the empty fields when attempting to submit a record</returns>
        private string EmptyFields()
        {
            try
            {
                // Create temp string to return.
                string tempString = String.Empty;

                if (String.IsNullOrEmpty(ShiftValue(String.Empty)))
                {
                    tempString = tempString + " [Work Shift]";
                }
                if (String.IsNullOrEmpty(unitInput.Text))
                {
                    tempString = tempString + " [Unit]";
                }
                if (String.IsNullOrEmpty(aircraftInput.Text))
                {
                    tempString = tempString + " [Aircraft Type]";
                }
                if (String.IsNullOrEmpty(tailnoInput.Text))
                {
                    tempString = tempString + " [Tail NO]";
                }
                if (String.IsNullOrEmpty(podnoInput.Text))
                {
                    tempString = tempString + " [Pod NO]";
                }
                if (String.IsNullOrEmpty(drdnoInput.Text))
                {
                    tempString = tempString + " [DRD NO]";
                }
                if (String.IsNullOrEmpty(torqueInput.Text))
                {
                    tempString = tempString + " [Torque Wrench]";
                }
                if (String.IsNullOrEmpty(stationInput.Text))
                {
                    tempString = tempString + " [Station]";
                }
                if (String.IsNullOrEmpty(exerciseInput.Text))
                {
                    tempString = tempString + " [Exercise]";
                }
                if (String.IsNullOrEmpty(codeInput.Text))
                {
                    tempString = tempString + " [Condition Code]";
                }

                return tempString;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'EmptyFields' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'EmptyFields' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return String.Empty;
            } 
        }

        /// <summary>
        /// Function to enable/disable inputs dependant on which button is checked. Split from the original function (InputModeChange_Click) as it needed to be called again without parameters, 
        /// the original required parameters for the click event and could not be overloaded.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void EnableDisableInputs()
        {
            try
            {
                // ClearSelections clears all inputs except work shift.
                ClearSelections();

                // Change available condition codes.
                ChangeConditionCodeOptions(false);
                trackingBTBox.IsEnabled = !(bool)uploadingButton.IsChecked;
                trackingNTBox.IsEnabled = !(bool)uploadingButton.IsChecked;
                trackingLabel.IsEnabled = !(bool)uploadingButton.IsChecked;
                drdnoInput.IsEnabled = (bool)uploadingButton.IsChecked;
                torqueInput.IsEnabled = (bool)uploadingButton.IsChecked;
                weightedBox.IsEnabled = false;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'EnableDisableInputs' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'EnableDisableInputs' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to execute the queries to upload the entered data into the database. Referenced in SyncButton_Click.
        /// </summary>
        /// <param name="row">The row to pull the query input params from</param>
        /// <param name="userInput">The user authorizing the input</param>
        /// <param name="connectDB">SQL Connection to database</param>
        /// <returns>None (Void)</returns>
        private void ExecuteQuery(DataRow row, string userInput, SqlConnection connectDB)
        {
            try
            {
                SqlCommand query = null;

                // Since rows where just being marked for deletion and not actually being deleted (attempted to update DT, but was not successful with built in methods)
                // check if row is marked for deletion before continuing.
                if (row.RowState != DataRowState.Deleted)
                {
                    // If a "DOWNLOAD" record.
                    if (row["ActionType"].ToString() == "DOWNLOAD" || row["ActionType"].ToString() == "CHANGE_DOWNLOAD")
                    {
                        query = new SqlCommand("INSERT INTO dbo.PodLoad_DOWNLOAD (AppVersion, Timestamp, WorkShift, Unit, TDY, AircraftType, TailNO, AircraftStatus, PodNO, Weighted, DRDNO, " +
                            "TorqueWrench, Station, Exercise, ConditionCode, Tracking, Comments, Authorizer, UpdateGUID) VALUES (@appVersion, @Timestamp, @shiftInput, @unitInput, @tdyInput, @aircraftInput, " +
                            "@tailnoInput, @statusInput, @podnoInput, @weightedInput, @drdnoInput, @torqueInput, @stationInput, @exerciseInput, @codeInput, @trackingInput, @commentInput, @userInput, @updateGUID)",
                            connectDB);
                    }
                    // If an "UPLOAD" record.
                    else
                    {
                        query = new SqlCommand("INSERT INTO dbo.PodLoad_UPLOAD (AppVersion, Timestamp, WorkShift, Unit, TDY, AircraftType, TailNO, AircraftStatus, PodNO, Weighted, DRDNO, " +
                            "TorqueWrench, Station, Exercise, ConditionCode, Tracking, Comments, Authorizer, UpdateGUID) VALUES (@appVersion, @Timestamp, @shiftInput, @unitInput, @tdyInput, @aircraftInput, " +
                            "@tailnoInput, @statusInput, @podnoInput, @weightedInput, @drdnoInput, @torqueInput, @stationInput, @exerciseInput, @codeInput, @trackingInput, @commentInput, @userInput, @updateGUID)",
                            connectDB);
                    }

                    query.Parameters.AddWithValue("@appVersion", appVersion);
                    query.Parameters.AddWithValue("@Timestamp", row["Timestamp"].ToString());
                    query.Parameters.AddWithValue("@shiftInput", StringFormat(row["WorkShift"].ToString()));
                    query.Parameters.AddWithValue("@unitInput", StringFormat(row["Unit"].ToString()));
                    query.Parameters.AddWithValue("@tdyInput", row["TDY"]);
                    query.Parameters.AddWithValue("@aircraftInput", StringFormat(row["AircraftType"].ToString()));
                    query.Parameters.AddWithValue("@tailnoInput", StringFormat(row["TailNO"].ToString()));
                    query.Parameters.AddWithValue("@statusInput", StringFormat(row["AircraftStatus"].ToString()));
                    query.Parameters.AddWithValue("@podnoInput", StringFormat(row["PodNO"].ToString()));
                    query.Parameters.AddWithValue("@weightedInput", row["Weighted"]);
                    query.Parameters.AddWithValue("@drdnoInput", StringFormat(row["DRDNO"].ToString()));
                    query.Parameters.AddWithValue("@torqueInput", StringFormat(row["TorqueWrench"].ToString()));
                    query.Parameters.AddWithValue("@stationInput", StringFormat(row["Station"].ToString()));
                    query.Parameters.AddWithValue("@exerciseInput", StringFormat(row["Exercise"].ToString()));
                    query.Parameters.AddWithValue("@codeInput", StringFormat(row["ConditionCode"].ToString().Substring(0, 4)));
                    query.Parameters.AddWithValue("@trackingInput", StringFormat(row["Tracking"].ToString()));

                    // If comment box is empty insert "BLANK"
                    if (row["Comments"].ToString() == String.Empty)
                    {
                        query.Parameters.AddWithValue("@commentInput", "BLANK");
                    }
                    else
                    {
                        query.Parameters.AddWithValue("@commentInput", row["Comments"].ToString());
                    }

                    query.Parameters.AddWithValue("@userInput", userInput);
                    query.Parameters.AddWithValue("@updateGUID", (Guid)row["UpdateGUID"]);
                    query.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ExecuteQuery' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ExecuteQuery' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to open the export window when the export button is pushed in the submenu.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void ExportButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Check for the admin password before continuing.
                if (AdminAccess())
                {
                    // Check if in debug mode.
                    if (debugMode)
                    {
                        //this.ShowMessageAsync("ALERT", "YOU ARE CURRENTLY IN DEBUG MODE. This window does not function without a connection to the database, if you attempt to perform a search the application" +
                        //" will hang (freeze) for 10 seconds as it tries to establish a connection. Please be patient, if it doesn't return to normal functionality restart the application.");
                        MessageBox.Show("YOU ARE CURRENTLY IN DEBUG MODE. This window does not function without a connection to the database, if you attempt to perform a search the application" +
                            " will hang (freeze) for 10 seconds as it tries to establish a connection. Please be patient, if it doesn't return to normal functionality restart the application.", "ALERT");
                    }

                    ExportWindow exportWindow = new ExportWindow(podloadLog, colorMode);
                    exportWindow.Owner = this;
                    exportWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ExportButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ExportButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to return either the change GUID or an empty GUID.
        /// </summary>
        /// <returns>GUID based on the use case</returns>
        private Guid GUIDValue()
        {
            try
            {
                if (tempGUID != Guid.Empty)
                {
                    return tempGUID;
                }

                return Guid.Empty;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'GUIDValue' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'GUIDValue' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Function for when the input mode is changed (upload/download). Function calls EnableDisableInputs which disables/enables inputs respective of mode and clears previous inputs.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void InputModeChange_Click(object sender, EventArgs e)
        {
            try
            {
                PerformSwap(false);
                EnableDisableInputs();
                ChangeConditionCodeOptions(false);
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'InputModeChange_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'InputModeChange_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to change the color of the menu and menu button when the main window loses focus.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void MetroWindow_Activation(object sender, EventArgs e)
        {
            try
            {
                Window window = sender as Window;

                // If window is not active, change menu/menu button to match inactive window title bar color.
                if (!window.IsActive)
                {
                    mainMenu.Background = (Brush)new BrushConverter().ConvertFromString("#808080");
                    mainMenuButton.Background = (Brush)new BrushConverter().ConvertFromString("#808080");
                }
                else
                {
                    if (colorMode == "LIGHT")
                    {
                        mainMenu.Background = (Brush)new BrushConverter().ConvertFromString("#FF83919f");
                        mainMenuButton.Background = (Brush)new BrushConverter().ConvertFromString("#FF83919f");
                    }
                    else if (colorMode == "DARK")
                    {
                        mainMenu.Background = (Brush)new BrushConverter().ConvertFromString("#FF576573");
                        mainMenuButton.Background = (Brush)new BrushConverter().ConvertFromString("#FF576573");
                    }
                }
            }
            catch(Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'MetroWindow_Activation' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'MetroWindow_Activation' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function for button click to toggle dark or light mode. Broken out into DarkLightToggle so it can be called outside of the button click.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void ModeToggleButton_Click(object sender, EventArgs e)
        {
            try
            {
                DarkLightToggle(false);
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ModeToggleButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ModeToggleButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to enable/disable the specified input fields when performing a DRD swap. Should only allow for editing of the DRD number.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void PerformSwap(bool perform)
        {
            try
            {
                // Temp var to use for later input statuses.
                bool tempBool = true;

                if (perform)
                {
                    tempBool = false;
                }

                unitInput.IsEnabled = tempBool;
                tdyBox.IsEnabled = tempBool;
                aircraftInput.IsEnabled = tempBool;
                tailnoInput.IsEnabled = tempBool;
                statusmreqButton.IsEnabled = tempBool;
                statussprButton.IsEnabled = tempBool;
                weightedBox.IsEnabled = tempBool;
                podnoInput.IsEnabled = tempBool;
                drdnoInput.IsEnabled = true;
                stationInput.IsEnabled = tempBool;
                exerciseInput.IsEnabled = tempBool;
                torqueInput.IsEnabled = tempBool;
                trackingBTBox.IsEnabled = tempBool;
                trackingNTBox.IsEnabled = tempBool;
                codeInput.Items.Clear();
                codeInput.Items.Add("DRDS - DRD Swap");
                codeInput.SelectedIndex = 0;
                codeInput.IsEnabled = tempBool;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'PerformSwap' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'PerformSwap' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to open the phone book webpage. Locally hosted.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void PhoneBookButton_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Directory.GetParent(Assembly.GetExecutingAssembly().Location) + "/ContactHTML.html") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'PhoneBookButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'PhoneBookButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function for when the save button is clicked. If any fields are empty require a input before continuing, else input values into datagridview and XML file and clear fields for new input.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                // If any required input fields are empty.
                if (String.IsNullOrEmpty(ShiftValue(String.Empty)) ||
                    String.IsNullOrEmpty(unitInput.Text) ||
                    String.IsNullOrEmpty(aircraftInput.Text) ||
                    String.IsNullOrEmpty(tailnoInput.Text) ||
                    String.IsNullOrEmpty(podnoInput.Text) ||
                    String.IsNullOrEmpty(drdnoInput.Text) ||
                    String.IsNullOrEmpty(torqueInput.Text) ||
                    String.IsNullOrEmpty(stationInput.Text) ||
                    String.IsNullOrEmpty(exerciseInput.Text) ||
                    String.IsNullOrEmpty(codeInput.Text))
                {
                    //this.ShowMessageAsync("ALERT", "The fields" + EmptyFields() + " are empty and need to be filled.");
                    MessageBox.Show("The fields" + EmptyFields() + " are empty and need to be filled.", "ALERT");
                }
                // Enter new row into datatable with given data.
                else
                {
                    dt.Rows.Add(new Object[]
                    {
                        false,
                        ShiftValue(String.Empty),
                        unitInput.Text,
                        tdyBox.IsChecked,
                        aircraftInput.Text,
                        tailnoInput.Text,
                        StatusValue(String.Empty),
                        podnoInput.Text,
                        weightedBox.IsChecked,
                        drdnoInput.Text,
                        torqueInput.Text,
                        stationInput.Text,
                        exerciseInput.Text,
                        codeInput.Text.Substring(0, 4),
                        TrackingValue(String.Empty),
                        commentInput.Text,
                        DateTime.UtcNow,
                        ActionValue(changeFlag),
                        GUIDValue()
                    });

                    dt.AcceptChanges();
                    displayedTable.AutoGenerateColumns = false;
                    displayedTable.ItemsSource = dt.DefaultView;
                    dt.WriteXml(fileName, XmlWriteMode.WriteSchema);
                    displayedTable.UnselectAll();
                    changeFlag = false;
                    PerformSwap(false);
                    ChangeConditionCodeOptions(changeFlag);
                    EnableDisableInputs();
                    deletedRows = 0;
                    UpdateRowCount();
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'SaveButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'SaveButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to change the selected aircraft via the respective unit.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any text changed event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void SelectAircraftFromUnit(object sender, TextChangedEventArgs e)
        {
            try
            {
                // Set up temp empty string to check by.
                string tempAircraft = String.Empty;

                // Check for inputed value in "Unit" textbox.
                if (unitInput.Text == "VIPER TFA" || unitInput.Text == "VIPER TF" || unitInput.Text == "TOMAHAWK" || unitInput.Text == "FALCON")
                {
                    tempAircraft = "F-16";
                }
                else if (unitInput.Text == "THUNDER CB" || unitInput.Text == "THUNDER TF")
                {
                    tempAircraft = "A-10";
                }
                else if (unitInput.Text == "STRIKE CB" || unitInput.Text == "STRIKE TF")
                {
                    tempAircraft = "F-15E";
                }
                else if (unitInput.Text == "EAGLE CB" || unitInput.Text == "EAGLE TF")
                {
                    tempAircraft = "F-15C";
                }
                else if (unitInput.Text == "DRAKEN")
                {
                    tempAircraft = "L-159";
                }

                // Check each of the available aircraft options and select the correct entry.
                foreach (object item in aircraftInput.Items)
                {
                    if (item.ToString().Contains(tempAircraft))
                    {
                        aircraftInput.SelectedItem = item;
                    }
                }

                // If no correct entry then do not select any entry and leave box blank.
                if (tempAircraft == String.Empty)
                {
                    aircraftInput.SelectedIndex = -1;
                }

                ChangeTailNOOptions();
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'SelectAircraftFromUnit' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'SelectAircraftFromUnit' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to return the respective shift string value dependant on button that is checked. If no value then return an empty string.
        /// Also performs the double duty of checking the correct shift button dependant on passed parameter when performing changes.
        /// </summary>
        /// <param name="workShift">String of work shift from change query</param>
        /// <returns>String of the respective work shift</returns>
        private string ShiftValue(string workShift)
        {
            try
            {
                if ((bool)shift1Button.IsChecked || workShift == "SHIFT1")
                {
                    shift1Button.IsChecked = true;
                    return "SHIFT1";
                }
                else if ((bool)shift2Button.IsChecked || workShift == "SHIFT2")
                {
                    shift2Button.IsChecked = true;
                    return "SHIFT2";
                }
                else if ((bool)shift3Button.IsChecked || workShift == "SHIFT3")
                {
                    shift3Button.IsChecked = true;
                    return "SHIFT3";
                }
                else if ((bool)shift4Button.IsChecked || workShift == "WKND")
                {
                    shift4Button.IsChecked = true;
                    return "WKND";
                }

                return String.Empty;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ShiftValue' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ShiftValue' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return null;
            }
        }

        /// <summary>
        /// Function to return the respective status string value dependant on button that is checked. If not value then return "N/A".
        /// Also performs the double duty of checking the correct status button dependant on passed parameter when performing changes.
        /// </summary>
        /// <param name="aircraftStatus">String of the aircraft status from change query</param>
        /// <returns>String of the respective aircraft status</returns>
        private string StatusValue(string aircraftStatus)
        {
            try
            {
                if ((bool)statusmreqButton.IsChecked || aircraftStatus == "MR")
                {
                    statusmreqButton.IsChecked = true;
                    return "MR";
                }
                else if ((bool)statussprButton.IsChecked || aircraftStatus == "SP")
                {
                    statussprButton.IsChecked = true;
                    return "SP";
                }

                return "N/A";
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'StatusValue' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'StatusValue' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return null;
            }
        }

        /// <summary>
        /// Function to format the string by stripping all whitespace, special characters and uppercasing all chars.
        /// </summary>
        /// <param name="str">String to format</param>
        /// <returns>Returns formatted string</returns>
        public string StringFormat(string str)
        {
            try
            {
                string tempStr = Regex.Replace(str, "[^a-zA-Z0-9_.]+", "");
                tempStr = Regex.Replace(tempStr, @"\s+", "");
                return tempStr.ToUpper();
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'StringFormat' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'StringFormat' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return null;
            }
        }

        /// <summary>
        /// Function to perform a DRD swap. Opens a new window.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void SwapButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if in debug mode.
                if (debugMode)
                {
                    //this.ShowMessageAsync("ALERT", "YOU ARE CURRENTLY IN DEBUG MODE. This window does not function without a connection to the database, if you attempt to perform a search the application" +
                    //" will hang (freeze) for 10 seconds as it tries to establish a connection. Please be patient, if it doesn't return to normal functionality restart the application.");
                    MessageBox.Show("YOU ARE CURRENTLY IN DEBUG MODE. This window does not function without a connection to the database, if you attempt to perform a search the application" +
                        " will hang (freeze) for 10 seconds as it tries to establish a connection. Please be patient, if it doesn't return to normal functionality restart the application.", "ALERT");
                }

                SwapWindow swapWindow = new SwapWindow(podloadLog, colorMode);
                swapWindow.Owner = this;
                swapWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'SwapButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'SwapButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to send results to the database via SQL queries, then archives the temp XML file by moving it to a new directory and renaming it with the timestamp of sync.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void SyncButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if all "Done" boxes are checked.
                if ((bool)DataGrid_Done())
                {
                    statusReadyLabel.Visibility = Visibility.Hidden;
                    statusLoadLabel.Visibility = Visibility.Visible;
                    syncBar.Visibility = Visibility.Visible;
                    syncBar.Value = 25;
                    string connectionString = ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString;

                    // Check if there is a valid connection to the database and the number of items is not zero.
                    if (UpdateRowCount() != 0)
                    {
                        syncBar.Value = 50;
                        string userInput = UserAuthenication();

                        // Check if userInput is empty (Currently used for authenication).
                        if (userInput != String.Empty)
                        {
                            syncBar.Value = 75;

                            // Check to see if we are in debug mode.
                            if (!debugMode)
                            {
                                SqlConnection connectDB = new SqlConnection(connectionString);
                                connectDB.Open();

                                foreach (DataRow row in dt.Rows)
                                {
                                    ExecuteQuery(row, userInput.ToString(), connectDB);
                                }

                                connectDB.Close();
                            }
                            else
                            {
                                //this.ShowMessageAsync("ALERT", "YOU ARE CURRENTLY IN DEBUG MODE. The entries will not be synced to the database, but it will still perform the local archiving process.");
                                MessageBox.Show("YOU ARE CURRENTLY IN DEBUG MODE. The entries will not be synced to the database, but it will still perform the local archiving process.", "ALERT");
                            }

                            // Archive synced XML file.
                            string moveFile = Directory.GetParent(Assembly.GetExecutingAssembly().Location) + "/Data/Archive/" + string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}.xml", DateTime.UtcNow);
                            File.Move(fileName, moveFile);
                            CreateFile(false);
                            syncBar.Value = 100;
                            //this.ShowMessageAsync("ALERT", "Sync Complete");
                            MessageBox.Show("Sync Complete.", "ALERT");
                            UpdateRowCount();
                        }
                        else
                        {
                            //this.ShowMessageAsync("ALERT", "You must enter your name to submit these records, please try again.");
                            MessageBox.Show("You must enter your name to submit these records, please try again.", "ALERT");
                        }
                    }
                    else
                    {
                        //this.ShowMessageAsync("ALERT", "No records to submit.");
                        MessageBox.Show("No records to submit.", "ALERT");
                    }
                }
            }
            catch (SqlException sqlEX)
            {
                podloadLog.Error(sqlEX.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "Could not establish connection to the database, please check your network connection and try again.");
                MessageBox.Show("Could not establish connection to the database, please check your network connection and try again.", "ERROR");
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'SyncButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'SyncButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
            finally
            {
                syncBar.Visibility = Visibility.Hidden;
                statusReadyLabel.Visibility = Visibility.Visible;
                statusLoadLabel.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// Function for grouping the tracking boxes together ala radiobuttons "groupname" attribute. Toggles the buttons so that both can not be pressed at the same time.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any routed event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void TrackingBox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckBox checkBox = sender as CheckBox;

                // Check sender's content to see what button has just been checked.
                if (checkBox.Content.ToString() == "BT")
                {
                    trackingNTBox.IsChecked = false;
                }
                else if (checkBox.Content.ToString() == "NT")
                {
                    trackingBTBox.IsChecked = false;
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'TrackingBox_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'TrackingBox_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to return the respective tracking string value dependant on button that is checked. If no value then return "N/A".
        /// Also performs the double duty of checking the correct tracking box dependant on passed parameter when performing changes.
        /// </summary>
        /// <param name="tracking">String of the tracking value from change query</param>
        /// <returns>String of the respective tracking value</returns>
        private string TrackingValue(string tracking)
        {
            try
            {
                if ((bool)trackingBTBox.IsChecked || tracking == "BT")
                {
                    trackingBTBox.IsChecked = true;
                    return "BT";
                }
                else if ((bool)trackingNTBox.IsChecked || tracking == "NT")
                {
                    trackingNTBox.IsChecked = true;
                    return "NT";
                }

                return "N/A";
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'TrackingValue' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'TrackingValue' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return null;
            }
        }

        /// <summary>
        /// Function to update the row count in the status bar.
        /// </summary>
        /// <returns>None (Void)</returns>
        private int UpdateRowCount()
        {
            try
            {
                // Kinda a jank fix, but deletedRows equals '1' or '0' depending on if a row has been deleted or not.
                // This is a fix since Items.Count does not update until after all events have been already fired.
                // Because of this the count was always off by one. This resolves that issue.
                rowCountLabel.Content = displayedTable.Items.Count - deletedRows;
                return displayedTable.Items.Count - deletedRows;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'UpdateRowCount' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'UpdateRowCount' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return 0;
            }
        }

        /// <summary>
        /// Function that recieves a user's input via InputBox and strips any whitespace and uppercases the entry and returns the modified value.
        /// </summary>
        /// <returns>A modified string of the inputed user authenication value</returns>
        private string UserAuthenication()
        {
            try
            {
                //var userInput = this.ShowInputAsync("ALERT", "Please enter your first initial, period, then last name below (Ex: A.Kim).");
                string userInput = Prompt.ShowDialog("Please enter your first initial, period, then last name below (Ex: A.Kim).", "USER AUTHENICATION");
                return Regex.Replace(userInput, @"\s+", "").ToUpper();
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'UserAuthenication' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'UserAuthenication' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
                return null;
            }
        }

        /// <summary>
        /// Function to delete a row from the XML file. Broken out into a seperate function so it can be called in multiple different contexts.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void XML_Delete()
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(fileName);
                xmlDoc.DocumentElement.RemoveChild(xmlDoc.DocumentElement.ChildNodes[displayedTable.SelectedIndex + 1]);
                xmlDoc.Save(fileName);

                // Update the row count in the status bar.
                deletedRows = 1;
                UpdateRowCount();
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'XML_Delete' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'XML_Delete' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        private void CAC_Credentials()
        {/*
            var Certs = new List<X509Certificate2>();
            var certsStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            certsStore.Open(OpenFlags.ReadWrite);
           
            if (certsStore.Certificates[0] != null)
            {
                X509Certificate2 cert = certsStore.Certificates[0];
                string test3 = cert.Subject;
                MessageBox.Show("NAME: " +  test3);
                certsStore.Remove(cert);
            }
            certsStore.Close();*/
        }
    }
}
//53B19D17B86453DDBE3E8F24C52E96A9
//09B7BAA5086E3F2FE3E2C537FC49DB6D38074626