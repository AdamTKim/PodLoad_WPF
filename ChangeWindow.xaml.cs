/////////////////////////////////////////////////////////////////////////////////////////
//53B19D17B86453DDBE3E8F24C52E96A9
//09B7BAA5086E3F2FE3E2C537FC49DB6D38074626
//Author: Adam Kim
//Created On: 8/25/2020
//Last Modified On: 11/5/2020
//Copyright: USAF // JT4 LLC
//Description: Change window of the PodLoad application
/////////////////////////////////////////////////////////////////////////////////////////
using ControlzEx.Theming;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.Controls;
using Serilog;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System;

namespace PodLoad_WPF
{
    /// <summary>
    /// Interaction logic for ChangeWindow.xaml
    /// </summary>
    public partial class ChangeWindow : MetroWindow
    {
        /// <summary>
        /// Global variables for the change datatable and for the logging file
        /// </summary>
        public DataTable dtChange = new DataTable();
        ILogger podloadLog;

        /// <summary>
        /// Initial function to initialize the change form, builds the datatable and changes the theme to "Dark Orange".
        /// </summary>
        /// <returns>None</returns>
        public ChangeWindow(ILogger podloadLog_Main, string colorMode)
        {
            try
            {
                podloadLog = podloadLog_Main;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                DataTableBuilder();
                InitializeComponent();

                // Set current theme to the respective dark mode/light mode selection.
                if (colorMode == "DARK")
                {
                    ThemeManager.Current.ChangeTheme(this, "Dark.Orange");
                }
                else if (colorMode == "LIGHT")
                {
                    ThemeManager.Current.ChangeTheme(this, "Light.Orange");
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ChangeWindow' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ChangeWindow' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to pass the values from the selected entry in the change datagrid to the main window so it can populate the respective fields.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any mouse event args sent by sender</param>
        /// <returns>None (Void)</returns>
        public void ChangeRowSelection_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DataRowView row = (DataRowView)changeTable.SelectedItem;

                // Checks if null to ensure that atleast one selection was made
                if ((bool)uploadingButton.IsChecked && row != null)
                {
                    ((MainWindow)this.Owner).ChangeModeMoveResults((Guid)row["Primary Key"], row["WorkShift"].ToString(), row["Unit"].ToString(), (bool)row["TDY"], row["AircraftType"].ToString(),
                        row["TailNO"].ToString(), row["AircraftStatus"].ToString(), row["PodNO"].ToString(), (bool)row["Weighted"], row["DRDNO"].ToString(), row["TorqueWrench"].ToString(),
                        row["Station"].ToString(), row["Exercise"].ToString(), row["ConditionCode"].ToString(), row["Tracking"].ToString(), row["Comments"].ToString(), "UPLOAD");
                    this.Close();
                }
                else if ((bool)downloadingButton.IsChecked && row != null)
                {
                    ((MainWindow)this.Owner).ChangeModeMoveResults((Guid)row["Primary Key"], row["WorkShift"].ToString(), row["Unit"].ToString(), (bool)row["TDY"], row["AircraftType"].ToString(),
                        row["TailNO"].ToString(), row["AircraftStatus"].ToString(), row["PodNO"].ToString(), (bool)row["Weighted"], row["DRDNO"].ToString(), row["TorqueWrench"].ToString(),
                        row["Station"].ToString(), row["Exercise"].ToString(), row["ConditionCode"].ToString(), row["Tracking"].ToString(), row["Comments"].ToString(), "DOWNLOAD");
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ChangeRowSelection_DoubleClick' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ChangeRowSelection_DoubleClick' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
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
                // Assign "empty" values to their respective fields
                unitInput.Text = String.Empty;
                podnoInput.Text = String.Empty;
                drdnoInput.Text = String.Empty;
                datetoInput.Text = String.Empty;
                datefromInput.Text = String.Empty;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ClearSelections' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ClearSelections' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
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
        /// Function to clear the current selection in the datagridview when clicked on an empty part of the table.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any mouse event args sent by sender</param>
        /// <return>None (Void)</return>
        private void Datagrid_Deselect(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DataGridRow row = changeTable.ItemContainerGenerator.ContainerFromItem(changeTable.SelectedItem) as DataGridRow;

                if (row != null && !row.IsMouseOver)
                {
                    changeTable.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'Datagrid_Deselect' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'Datagrid_Deselect' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to build the datatable used to store the results from the SELECT query in QueryBuilder.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void DataTableBuilder()
        {
            try
            {
                dtChange.TableName = "PodFormChangeTable";
                dtChange.Columns.Add("Primary Key", typeof(Guid));
                dtChange.Columns.Add("Timestamp", typeof(DateTime));
                dtChange.Columns.Add("WorkShift", typeof(string));
                dtChange.Columns.Add("Unit", typeof(string));
                dtChange.Columns.Add("TDY", typeof(bool));
                dtChange.Columns.Add("AircraftType", typeof(string));
                dtChange.Columns.Add("TailNO", typeof(string));
                dtChange.Columns.Add("AircraftStatus", typeof(string));
                dtChange.Columns.Add("PodNO", typeof(string));
                dtChange.Columns.Add("Weighted", typeof(bool));
                dtChange.Columns.Add("DRDNO", typeof(string));
                dtChange.Columns.Add("TorqueWrench", typeof(string));
                dtChange.Columns.Add("Station", typeof(string));
                dtChange.Columns.Add("Exercise", typeof(string));
                dtChange.Columns.Add("ConditionCode", typeof(string));
                dtChange.Columns.Add("Tracking", typeof(string));
                dtChange.Columns.Add("Comments", typeof(string));
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataTableBuilder' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataTableBuilder' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function for when the input mode is changed (upload/download). Function disables/enables inputs respective of mode and clears previous inputs.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void InputModeChange_Click(object sender, EventArgs e)
        {
            try
            {
                ClearSelections();
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'InputModeChange_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'InputModeChange_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to construct the "change" query based off of user input, retreives the results then places them in a datatable.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void QueryBuilder()
        {
            try
            {
                // Clear table before inputing results.
                dtChange.Clear();

                // Connection to the database.
                string queryString = String.Empty;
                string connectionString = ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString;
                SqlConnection connectDB = new SqlConnection(connectionString);
                connectDB.Open();

                // Start constructing the query dependant on input values and the table that it needs to search.
                if ((bool)uploadingButton.IsChecked)
                {
                    queryString = "SELECT [Primary Key], Timestamp, WorkShift, Unit, TDY, AircraftType, TailNO, AircraftStatus, PodNO, Weighted, DRDNO, TorqueWrench, Station, Exercise, ConditionCode, " +
                        "Tracking, Comments FROM dbo.PodLoad_UPLOAD WHERE ";
                }
                else
                {
                    queryString = "SELECT [Primary Key], Timestamp, WorkShift, Unit, TDY, AircraftType, TailNO, AircraftStatus, PodNO, Weighted, DRDNO, TorqueWrench, Station, Exercise, ConditionCode, " +
                        "Tracking, Comments FROM dbo.PodLoad_DOWNLOAD WHERE ";
                }

                // If inputbox contains "N/A" skip that entry as those fields are not relevant.
                if (unitInput.Text != String.Empty && unitInput.Text != "N/A")
                {
                    queryString = queryString + "Unit = @unitInput";

                    if (podnoInput.Text != String.Empty || drdnoInput.Text != String.Empty && drdnoInput.Text != "N/A")
                    {
                        queryString = queryString + " AND ";
                    }
                }

                if (podnoInput.Text != String.Empty)
                {
                    queryString = queryString + "PodNO = @podnoInput";

                    if (drdnoInput.Text != String.Empty && drdnoInput.Text != "N/A")
                    {
                        queryString = queryString + " AND ";
                    }
                }

                if (drdnoInput.Text != String.Empty && drdnoInput.Text != "N/A")
                {
                    queryString = queryString + "DRDNO = @drdnoInput";
                }

                // Input param values for constructed parameterized query.
                queryString = queryString + " AND Timestamp BETWEEN @datefromInput AND @datetoInput";
                SqlCommand changeQuery = new SqlCommand(queryString, connectDB);
                changeQuery.Parameters.AddWithValue("@unitInput", ((MainWindow)this.Owner).StringFormat(unitInput.Text));
                changeQuery.Parameters.AddWithValue("@podnoInput", ((MainWindow)this.Owner).StringFormat(podnoInput.Text));
                changeQuery.Parameters.AddWithValue("@drdnoInput", ((MainWindow)this.Owner).StringFormat(drdnoInput.Text));
                changeQuery.Parameters.AddWithValue("@datefromInput", datefromInput.SelectedDate.Value.Date.ToString("yyyy-MM-dd") + " 00:00:01");
                changeQuery.Parameters.AddWithValue("@datetoInput", datetoInput.SelectedDate.Value.Date.ToString("yyyy-MM-dd") + " 23:59:59");

                // Run query, save results to a dataadapter, then place results into the datatable.
                using (SqlDataAdapter da = new SqlDataAdapter(changeQuery))
                {
                    int returnedRows = da.Fill(dtChange);

                    if (returnedRows < 1)
                    {
                        //this.ShowMessageAsync("ALERT", "No results found.");
                        MessageBox.Show("No results found.", "ALERT");
                    }
                }

                dtChange.DefaultView.Sort = "[Timestamp] ASC";
                changeTable.ItemsSource = dtChange.DefaultView;
                connectDB.Close();
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
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'QueryBuilder' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'QueryBuilder' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function to run when the submit button is clicked. Checks for valid inputs and calls the QueryBuilder function.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void SearchButton_Click(object sender, EventArgs e)
        {
            try
            {
                statusReadyLabel.Visibility = Visibility.Hidden;
                statusLoadLabel.Visibility = Visibility.Visible;
                syncBar.Visibility = Visibility.Visible;
                syncBar.Value = 25;

                if (unitInput.Text == String.Empty && podnoInput.Text == String.Empty && drdnoInput.Text == String.Empty)
                {
                    //this.ShowMessageAsync("ALERT", "You must enter a value for one of the available fields.");
                    MessageBox.Show("You must enter a value for one of the available fields.", "ALERT");
                }
                else
                {
                    if (datefromInput.Text == String.Empty || datetoInput.Text == String.Empty)
                    {
                        //this.ShowMessageAsync("ALERT", "You must enter a valid date range.");
                        MessageBox.Show("You must enter a valid date range.", "ALERT");
                    }
                    else
                    {
                        syncBar.Value = 50;
                        QueryBuilder();
                        ClearSelections();
                    }
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'SearchButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'SearchButton_Click' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
            finally
            {
                syncBar.Visibility = Visibility.Hidden;
                statusReadyLabel.Visibility = Visibility.Visible;
                statusLoadLabel.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// Function to check if enter has been pressed in a searchable textbox.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any key event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void TextboxEnter_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter || e.Key == Key.Return)
                {
                    SearchButton_Click(this, new EventArgs());
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'TextboxEnter_KeyDown' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'TextboxEnter_KeyDown' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }
    }
}
//53B19D17B86453DDBE3E8F24C52E96A9
//09B7BAA5086E3F2FE3E2C537FC49DB6D38074626