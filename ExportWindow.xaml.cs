/////////////////////////////////////////////////////////////////////////////////////////
//53B19D17B86453DDBE3E8F24C52E96A9
//09B7BAA5086E3F2FE3E2C537FC49DB6D38074626
//Author: Adam Kim
//Created On: 8/25/2020
//Last Modified On: 11/5/2020
//Copyright: USAF // JT4 LLC
//Description: Export window of the PodLoad application
/////////////////////////////////////////////////////////////////////////////////////////
using ControlzEx.Theming;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using Serilog;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System;

namespace PodLoad_WPF
{
    /// <summary>
    /// Interaction logic for ExportWindow.xaml
    /// </summary>
    public partial class ExportWindow : MetroWindow
    {
        /// <summary>
        /// Global variables for the change datatable and for the logging file
        /// </summary>
        public DataTable dtExport = new DataTable();
        ILogger podloadLog;
        int returnedUpRows = 0;
        int returnedDownRows = 0;

        public ExportWindow(ILogger podloadLog_Main, string colorMode)
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
                    ThemeManager.Current.ChangeTheme(this, "Dark.Violet");
                }
                else if (colorMode == "LIGHT")
                {
                    ThemeManager.Current.ChangeTheme(this, "Light.Violet");
                }
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'ChangeWindow' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'ExportWindow' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
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
                DataGridRow row = exportTable.ItemContainerGenerator.ContainerFromItem(exportTable.SelectedItem) as DataGridRow;

                if (row != null && !row.IsMouseOver)
                {
                    exportTable.SelectedItem = null;
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
                dtExport.TableName = "PodFormExportTable";
                dtExport.Columns.Add("Timestamp", typeof(DateTime));
                dtExport.Columns.Add("WorkShift", typeof(string));
                dtExport.Columns.Add("Unit", typeof(string));
                dtExport.Columns.Add("TDY", typeof(bool));
                dtExport.Columns.Add("AircraftType", typeof(string));
                dtExport.Columns.Add("TailNO", typeof(string));
                dtExport.Columns.Add("AircraftStatus", typeof(string));
                dtExport.Columns.Add("PodNO", typeof(string));
                dtExport.Columns.Add("Weighted", typeof(bool));
                dtExport.Columns.Add("DRDNO", typeof(string));
                dtExport.Columns.Add("TorqueWrench", typeof(string));
                dtExport.Columns.Add("Station", typeof(string));
                dtExport.Columns.Add("Exercise", typeof(string));
                dtExport.Columns.Add("ConditionCode", typeof(string));
                dtExport.Columns.Add("Tracking", typeof(string));
                dtExport.Columns.Add("Comments", typeof(string));
                dtExport.Columns.Add("Authorizer", typeof(string));
                dtExport.Columns.Add("UpdateGUID", typeof(string));
                dtExport.Columns.Add("ActionType", typeof(string));
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'DataTableBuilder' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'DataTableBuilder' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }

        /// <summary>
        /// Function for exporting the records found in the datatable.
        /// </summary>
        /// <param name="sender">Object that sent the action</param>
        /// <param name="e">Any event args sent by sender</param>
        /// <returns>None (Void)</returns>
        private void ExportButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Set up dialog box to save file.
                SaveFileDialog exportDLG = new SaveFileDialog();
                exportDLG.DefaultExt = ".csv";
                exportDLG.Filter = "CSV Documents (.csv)|*.csv";

                // Check if number of rows to export is zero.
                if (dtExport.Rows.Count != 0)
                {
                    // If save location has been selected from dialog box.
                    if ((bool)exportDLG.ShowDialog())
                    {
                        // Open streamwriter to save file contents.
                        StreamWriter sw = new StreamWriter(exportDLG.FileName, false);

                        // Setup columns in CSV file.
                        for (int i = 0; i < dtExport.Columns.Count; i++)
                        {
                            sw.Write(dtExport.Columns[i]);

                            if (i < dtExport.Columns.Count - 1)
                            {
                                sw.Write(",");
                            }
                        }

                        sw.Write(sw.NewLine);

                        // Write each row to the CSV file.
                        foreach (DataRow dr in dtExport.Rows)
                        {
                            for (int i = 0; i < dtExport.Columns.Count; i++)
                            {
                                if (!Convert.IsDBNull(dr[i]))
                                {
                                    string value = dr[i].ToString();

                                    if (value.Contains(","))
                                    {
                                        value = String.Format("\"{0}\"", value);
                                        sw.Write(value);
                                    }
                                    else
                                    {
                                        sw.Write(dr[i].ToString());
                                    }
                                }

                                if (i < dtExport.Columns.Count - 1)
                                {
                                    sw.Write(",");
                                }
                            }

                            sw.Write(sw.NewLine);
                        }

                        // Close the streamwriter.
                        sw.Close();
                    }
                }
                else
                {
                    //this.ShowMessageAsync("ALERT", "There are no records to export");
                    MessageBox.Show("There are no records to export", "ALERT");
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
        /// Function to construct the "change" query based off of user input, retreives the results then places them in a datatable.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void QueryBuilder()
        {
            try
            {
                // Clear datatable and set number of returned rows back to zero.
                dtExport.Clear();
                returnedDownRows = 0;
                returnedUpRows = 0;

                // Connection to the database.
                string connectionString = ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString;
                SqlConnection connectDB = new SqlConnection(connectionString);
                connectDB.Open();

                // Construct queries to return both uploads and downloads.
                string queryStringUP = "SELECT Timestamp, WorkShift, Unit, TDY, AircraftType, TailNO, AircraftStatus, PodNO, Weighted, DRDNO, TorqueWrench, Station, Exercise, ConditionCode, Tracking, " +
                    "Comments, Authorizer, UpdateGUID, 'UPLOAD' AS ActionType FROM dbo.PodLoad_UPLOAD WHERE Timestamp BETWEEN @datefromInput AND @datetoInput";
                string queryStringDOWN = "SELECT Timestamp, WorkShift, Unit, TDY, AircraftType, TailNO, AircraftStatus, PodNO, Weighted, DRDNO, TorqueWrench, Station, Exercise, ConditionCode, Tracking, " +
                    "Comments, Authorizer, UpdateGUID, 'DOWNLOAD' AS ActionType FROM dbo.PodLoad_DOWNLOAD WHERE Timestamp BETWEEN @datefromInput AND @datetoInput";
                SqlCommand exportQueryUP = new SqlCommand(queryStringUP, connectDB);
                SqlCommand exportQueryDOWN = new SqlCommand(queryStringDOWN, connectDB);
                exportQueryUP.Parameters.AddWithValue("@datefromInput", datefromInput.SelectedDate.Value.Date.ToString("yyyy-MM-dd") + " 00:00:01");
                exportQueryUP.Parameters.AddWithValue("@datetoInput", datetoInput.SelectedDate.Value.Date.ToString("yyyy-MM-dd") + " 23:59:59");
                exportQueryDOWN.Parameters.AddWithValue("@datefromInput", datefromInput.SelectedDate.Value.Date.ToString("yyyy-MM-dd") + " 00:00:01");
                exportQueryDOWN.Parameters.AddWithValue("@datetoInput", datetoInput.SelectedDate.Value.Date.ToString("yyyy-MM-dd") + " 23:59:59");

                // Run query, save results to a dataadapter, then place results into the datatable.
                SqlDataAdapter daUP = new SqlDataAdapter(exportQueryUP);
                SqlDataAdapter daDOWN = new SqlDataAdapter(exportQueryDOWN);
                returnedUpRows = daUP.Fill(dtExport);
                returnedDownRows = daDOWN.Fill(dtExport);

                if ((returnedDownRows + returnedUpRows) < 1)
                {
                    //this.ShowMessageAsync("ALERT", "No results found.");
                    MessageBox.Show("No results found.", "ALERT");
                }

                dtExport.DefaultView.Sort = "[Timestamp] ASC";
                exportTable.ItemsSource = dtExport.DefaultView;
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

                if (datefromInput.Text == String.Empty || datetoInput.Text == String.Empty)
                {
                    //this.ShowMessageAsync("ALERT", "You must enter a valid date range.");
                    MessageBox.Show("You must enter a valid date range.", "ALERT");
                }
                else
                {
                    syncBar.Value = 50;
                    QueryBuilder();
                    UpdateTotals();
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
        /// Function to update the upload/download totals in the export window.
        /// </summary>
        /// <returns>None (Void)</returns>
        private void UpdateTotals()
        {
            try
            {
                totalDownCountLabel.Content = returnedDownRows;
                totalUpCountLabel.Content = returnedUpRows;
            }
            catch (Exception ex)
            {
                podloadLog.Error(ex.ToString() + "\n");
                //this.ShowMessageAsync("ERROR", "There was a failure in the 'UpdateTotals' function. Restart the application and try again or contact your system administrator if the problem persists.");
                MessageBox.Show("There was a failure in the 'UpdateTotals' function. Restart the application and try again or contact your system administrator if the problem persists.", "ERROR");
            }
        }
    }
}
//53B19D17B86453DDBE3E8F24C52E96A9
//09B7BAA5086E3F2FE3E2C537FC49DB6D38074626