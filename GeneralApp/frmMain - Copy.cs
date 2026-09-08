using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BAFL_Campaign_App
{
    public partial class frmMain : Form
    {
        Configuration AppConfig;
        Stopwatch objStopWatch;
        Thread ThreadProcessBAFL;

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                MessageBox.Show("Another Instance of this application is already running", "Multiple Instances are Forbidden", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                Application.Exit();
            }

            btnStart.Enabled = true;
            btnExit.Enabled = true;
            btnConfiguration.Enabled = true;
            btnStop.Enabled = false;

            tmrProcess.Enabled = false;
            tmrUpdateStatus.Enabled = false;

            AppConfig = new Configuration();
            this.Text = AppConfig.EngineName;
            lblAppName.Text = AppConfig.EngineName;

            if (string.IsNullOrEmpty(tbStartTime.Text))
            {
                tbStartTime.Text = DateTime.Now.ToString("dd-MMM-yyyy hh:mm:ss tt");
            }

            if (string.IsNullOrEmpty(tbTimeElapsed.Text))
            {
                tbTimeElapsed.Text = "0 : 00 : 00";
            }

            if (string.IsNullOrEmpty(tbTransactions.Text))
            {
                tbTransactions.Text = "0";
            }

            tmrUpdateStatus.Interval = 60000;
            tmrTime.Interval = Convert.ToInt16(AppConfig.RefreshTime) * 1000;
            objStopWatch = new Stopwatch();
            ThreadProcessBAFL = new Thread(ProcessBAFL);
        }

        private void tmrProcess_Tick(object sender, EventArgs e)
        {
            //ProcessShell_V3();
            //ProcessCCF();

            if (ThreadProcessBAFL.IsAlive == false)
            {
                ThreadProcessBAFL = new Thread(ProcessBAFL);
                ThreadProcessBAFL.IsBackground = false;
                ThreadProcessBAFL.Start();
            }
        }
        private void ProcessBAFL()
        {
            string Sql;
            DAL objDAL = new DAL();
            Configuration _AppConfig = new Configuration();
            DataTable dt;

            string _ID;
            string _Mobile;
            string _Telco;
            string _MsgId;
            string _Msg;
            string _SCode;
            string _msgDate;
            string _sResponse = "0";

            try
            {
                Sql = "select top " + AppConfig.TopRecord + " id, sender, telco, msgid, msg, SCode, convert(varchar, format(adate, 'dd-MMM-yyyy hh:mm:ss tt'))  as msgDate " +
                        " from tblIncoming";
                dt = objDAL.doSelect(Sql, _AppConfig.AppConnectionString);

                foreach (DataRow MORow in dt.Rows)
                {
                    _ID = "";
                    _Mobile = "";
                    _Telco = "";
                    _MsgId = "";
                    _Msg = "";
                    _SCode = "";
                    _msgDate = string.Empty;

                    _ID = MORow["id"].ToString();
                    _Mobile = MORow["sender"].ToString();
                    _Telco = MORow["telco"].ToString();
                    _MsgId = MORow["msgid"].ToString();
                    _Msg = MORow["msg"].ToString();
                    _SCode = MORow["SCode"].ToString();
                    _msgDate = MORow["msgDate"].ToString();

                    _Mobile = _Mobile.Replace("+", "");
                    _Mobile = _Mobile.Replace("-", "");
                    _Mobile = _Mobile.Trim();

                    _Msg = _Msg.Replace("'", " ");
                    _Msg = _Msg.Replace("[", " ");
                    _Msg = _Msg.Replace("]", " ");
                    _Msg = _Msg.Replace("{", " ");
                    _Msg = _Msg.Replace("}", " ");
                    _Msg = _Msg.Replace("(", " ");
                    _Msg = _Msg.Replace(")", " ");
                    _Msg = _Msg.Replace("<", " ");
                    _Msg = _Msg.Replace(">", " ");
                    _Msg = _Msg.Replace(".", " ");
                    _Msg = _Msg.Replace(",", " ");
                    _Msg = _Msg.Replace("-", " ");
                    _Msg = _Msg.Replace("_", " ");
                    _Msg = _Msg.Replace("&", " ");
                    _Msg = _Msg.Replace("*", " ");
                    _Msg = _Msg.Replace("=", " ");
                    _Msg = _Msg.Replace("!", " ");
                    _Msg = _Msg.Replace("#", " ");
                    _Msg = _Msg.Replace("@", " ");
                    _Msg = _Msg.Replace("$", " ");
                    _Msg = _Msg.Replace("%", " ");
                    _Msg = _Msg.Replace(Convert.ToString((char)34), " ");
                    _Msg = _Msg.ToLower();
                    _Msg = _Msg.Trim();

                    ShowActivity(ActivityType.AddTransactionList, string.Format("Mobile No  : {0}", _Mobile));
                    ShowActivity(ActivityType.AddTransactionList, string.Format("Short Code : {0}", _SCode));
                    ShowActivity(ActivityType.AddTransactionList, string.Format("Message  : {0}", _Msg));

                    if (_SCode == "9902")
                    {
                        string otp = _Msg.Replace(" ", "");

                        // 1st Service: Message is purely numbers -> Send OTP to Gateway
                        if (!string.IsNullOrEmpty(otp) && IsNumeric(otp))
                        {
                            SendBAFLOtpToGateway(_Mobile, otp);
                        }
                        // 2nd Service: Non-numeric message -> Process prefix inquiry (BAPULL logic without prefix requirement)
                        else
                        {
                            string fullMessage = _Msg.Trim();

                            // Order prefixes from LONGEST to SHORTEST
                            string[] prefixes = new string[] {
                                "Internet Off", "Internet On", "Alfa Block", "DC Block",
                                "CC Help", "CCBPR", "CCBPS", "Raast", "Orbit",
                                "CCMS", "BPR", "BPS", "CCP", "CHQ",
                                "Help", "More", "AB", "CC", "Cu", "MS", "AD"
                            };

                            string matchedActivity = string.Empty;
                            string matchedAccount = string.Empty;

                            // Extract the first word to test exact keyword matching
                            string[] messageWords = fullMessage.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            string firstWord = messageWords.Length > 0 ? messageWords[0] : fullMessage;

                            foreach (string prefix in prefixes)
                            {
                                if (firstWord.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                                    fullMessage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                {
                                    matchedActivity = prefix;
                                    break;
                                }
                            }

                            // Fallback search if no prefix matched at start
                            if (string.IsNullOrEmpty(matchedActivity))
                            {
                                foreach (string prefix in prefixes)
                                {
                                    if (fullMessage.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        matchedActivity = prefix;
                                        break;
                                    }
                                }
                            }

                            // Extract numerical account details if present
                            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(fullMessage, @"\d+");
                            if (match.Success)
                            {
                                matchedAccount = match.Value;
                            }

                            // Static DB Check for matched prefix
                            string staticResponse = GetStaticResponseFromDb(matchedActivity);

                            if (!string.IsNullOrEmpty(staticResponse))
                            {
                                string staticCode = "STATIC_00";

                                ShowActivity(ActivityType.AddTransactionList, $"BAPULL Static Handled [{_Mobile}]. Response for prefix: {matchedActivity}");
                                CreateLog($"[BAPULL STATIC] Mobile: {_Mobile} | Activity: {matchedActivity} | Queued MT: {staticResponse}", "BAPULL_LOG", AppConfig.LogPath);

                                InsertMTMessage(_MsgId, _Mobile, _Msg, staticResponse, _Telco, _SCode);
                                InsertBapullDbLog(_Mobile, _MsgId, fullMessage, matchedActivity, staticCode, staticResponse, _Telco, _SCode);
                            }
                            else
                            {
                                // Proceed with normal API gateway call
                                SendBapullToGateway(_Mobile, matchedActivity, matchedAccount, _MsgId, fullMessage, _Telco, _SCode);
                            }
                        }
                    }

                    if (_sResponse == "0")
                    {
                        Sql = "Delete from  tblIncoming WHERE ID = '" + _ID + "' ";
                        objDAL.doExecute(Sql, _AppConfig.AppConnectionString);
                    }

                    ShowActivity(ActivityType.AddTransaction, "1");
                    ShowActivity(ActivityType.AddTransactionList, "------------------------------------");
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, string.Format("[ProcessBAFL]: {0}", ex.Message));
                CreateLog(string.Format("[ProcessBAFL]: {0}", ex.Message), "Campaign", AppConfig.LogPath);
            }
        }

        private string GetStaticResponseFromDb(string activity)
        {
            if (string.IsNullOrEmpty(activity))
                return null;

            string responseMessage = null;

            try
            {
                // Replace AppConfig.ConnectionString with your actual connection string variable
                using (System.Data.SqlClient.SqlConnection conn = new System.Data.SqlClient.SqlConnection(AppConfig.AppConnectionString))
                {
                    string query = "SELECT ResponseMessage FROM BAFLDB..BapullStaticResponses WHERE LOWER(Prefix) = LOWER(@Prefix) AND IsActive = 1";

                    using (System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Prefix", activity);
                        conn.Open();

                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            responseMessage = result.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, $"[GetStaticResponseFromDb Error]: {ex.Message}");
                CreateLog($"[GetStaticResponseFromDb Error]: {ex.Message}", "BAPULL_LOG", AppConfig.LogPath);
            }

            return responseMessage;
        }

        private void SendBapullToGateway(string mobileNo, string activity, string accountData, string msgId, string originalMsg, string telco, string shortCode)
        {
            
            try
            {
                string soapEndpoint = "http://xxx.xxx.xxx.xx:Port/PullSMSService?wsdl";
                string safeOriginalMsg = System.Security.SecurityElement.Escape(originalMsg ?? string.Empty);

                string soapEnvelope = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:pul=""http://PullSMSService"">
   <soapenv:Header/>
   <soapenv:Body>
      <pul:BAFInquiry>
         <AccountNumber>{accountData}</AccountNumber>
         <MobileNumber>{mobileNo}</MobileNumber>
         <Activity>{activity}</Activity>
         <Field1></Field1>
         <Field2></Field2>
      </pul:BAFInquiry>
   </soapenv:Body>
</soapenv:Envelope>";

                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(soapEndpoint);
                request.Headers.Add("SOAPAction", "\"BAF Inquiry\"");
                request.ContentType = "text/xml; charset=utf-8";
                request.Method = "POST";

                using (System.IO.Stream stream = request.GetRequestStream())
                {
                    byte[] content = System.Text.Encoding.UTF8.GetBytes(soapEnvelope);
                    stream.Write(content, 0, content.Length);
                }

                using (System.Net.WebResponse response = request.GetResponse())
                {
                    using (System.IO.StreamReader rd = new System.IO.StreamReader(response.GetResponseStream()))
                    {
                        string soapResult = rd.ReadToEnd();

                        string gatewayMessage = string.Empty;
                        string gatewayCode = string.Empty;

                        try
                        {
                            System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();
                            xmlDoc.LoadXml(soapResult);

                            System.Xml.XmlNodeList codeNodes = xmlDoc.GetElementsByTagName("Resp_CD");
                            if (codeNodes.Count > 0)
                            {
                                gatewayCode = codeNodes[0].InnerText;
                            }

                            System.Xml.XmlNodeList descNodes = xmlDoc.GetElementsByTagName("Resp_Desc");
                            if (descNodes.Count > 0)
                            {
                                gatewayMessage = descNodes[0].InnerText;
                            }
                        }
                        catch (Exception xmlEx)
                        {
                            CreateLog($"[XML Parse Error]: {xmlEx.Message} | Raw Response: {soapResult}", "BAPULL_LOG", AppConfig.LogPath);
                        }

                        ShowActivity(ActivityType.AddTransactionList, $"BAPULL Sent [{mobileNo}]. Response: {gatewayCode} - {gatewayMessage}");

                        if (!string.IsNullOrEmpty(gatewayMessage))
                        {
                            InsertMTMessage(msgId, mobileNo, originalMsg, gatewayMessage, telco, shortCode);
                            CreateLog($"[BAPULL SUCCESS] Mobile: {mobileNo} | Code: {gatewayCode} | Queued MT: {gatewayMessage}", "BAPULL_LOG", AppConfig.LogPath);
                        }
                        else
                        {
                            CreateLog($"[BAPULL FAIL] No Resp_Desc found. Raw XML: {soapResult}", "BAPULL_LOG", AppConfig.LogPath);
                        }

                        InsertBapullDbLog(mobileNo, msgId, originalMsg, activity, gatewayCode, gatewayMessage, telco, shortCode);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, string.Format("[SendBapullToGateway Error]: {0}", ex.Message));
                CreateLog(string.Format("[SendBapullToGateway Error]: {0}", ex.Message), "BAPULL_LOG", AppConfig.LogPath);

                // --- NEW: LOG EXCEPTIONS TO THE DATABASE TOO ---
                InsertBapullDbLog(mobileNo, msgId, originalMsg, activity, "ERR", $"ERROR: {ex.Message}", telco, shortCode);
            }
        }

        
        private void InsertMTMessage(string msgId, string mobile, string originalMsg, string smsText, string telco, string shortCode)
        {
            try
            {
                // Using the existing connection string from your AppConfig
                using (System.Data.SqlClient.SqlConnection conn = new System.Data.SqlClient.SqlConnection(AppConfig.AppConnectionString))
                {
                    using (System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand("BAFLDB..sp_SendMOMT", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@MsgID", msgId);
                        cmd.Parameters.AddWithValue("@Mobile", mobile);
                        cmd.Parameters.AddWithValue("@Msg", originalMsg);  // The original message user sent (e.g. "bapull AB 1234")
                        cmd.Parameters.AddWithValue("@SMS", smsText);      // The message received from the SOAP API
                        cmd.Parameters.AddWithValue("@SMSType", "Text");
                        cmd.Parameters.AddWithValue("@ShortCode", shortCode);
                        cmd.Parameters.AddWithValue("@Mask", shortCode);
                        cmd.Parameters.AddWithValue("@MTTable", "");       // Passing empty as your SP handles this logic via @Telco
                        cmd.Parameters.AddWithValue("@Telco", telco);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, string.Format("[InsertMTMessage Error]: {0}", ex.Message));
                CreateLog(string.Format("[InsertMTMessage Error]: {0}", ex.Message), "BAPULL_LOG", AppConfig.LogPath);
            }
        }

        private void InsertBapullDbLog(string mobileNo, string msgId, string userMessage, string activityMatched, string gatewayCode, string extractedMessage, string telco, string shortCode)
        {
            try
            {
                using (System.Data.SqlClient.SqlConnection conn = new System.Data.SqlClient.SqlConnection(AppConfig.AppConnectionString))
                {
                    // Removed GatewayResponse, added GatewayCode
                    string sql = @"INSERT INTO BAFLDB..tblBapull_Logs 
                           (MobileNo, MsgID, UserMessage, Activity, GatewayCode, ResponceMessage, Telco, ShortCode) 
                           VALUES 
                           (@MobileNo, @MsgID, @UserMessage, @Activity, @GatewayCode, @ResponceMessage, @Telco, @ShortCode)";

                    using (System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@MobileNo", mobileNo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@MsgID", msgId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserMessage", userMessage ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Activity", activityMatched ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@GatewayCode", gatewayCode ?? (object)DBNull.Value); // NEW CODE PARAMETER
                        cmd.Parameters.AddWithValue("@ResponceMessage", extractedMessage ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Telco", telco ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ShortCode", shortCode ?? (object)DBNull.Value);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                CreateLog($"[DB LOG ERROR]: {ex.Message} | Mobile: {mobileNo}", "BAPULL_LOG", AppConfig.LogPath);
            }
        }
        private void SendBAFLOtpToGateway(string mobileNo, string otp)
        {
            try
            {
            
                string soapEndpoint = "http://xxx.xxx.xxx.xx:Port/TwoWaySMSService/TwoWaySMS.asmx?op=SendOTACBacktoGateway";
                string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">  
                        <soap:Body>    
                            <SendOTACBacktoGateway xmlns=""http://tempuri.org/"">      
                                <Message>{otp}</Message>      
                                <MobileNo>{mobileNo}</MobileNo>    
                            </SendOTACBacktoGateway>  
                        </soap:Body>
                    </soap:Envelope>";

                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(soapEndpoint);
                request.Headers.Add("SOAPAction", "\"http://tempuri.org/SendOTACBacktoGateway\"");
                request.ContentType = "text/xml; charset=utf-8";
                request.Method = "POST";

                using (System.IO.Stream stream = request.GetRequestStream())
                {
                    byte[] content = System.Text.Encoding.UTF8.GetBytes(soapEnvelope);
                    stream.Write(content, 0, content.Length);
                }

                using (System.Net.WebResponse response = request.GetResponse())
                {
                    using (System.IO.StreamReader rd = new System.IO.StreamReader(response.GetResponseStream()))
                    {
                        // Read the full XML response
                        string soapResult = rd.ReadToEnd();

                        string responseCode = "Unknown";
                        string responseDesc = "No Description";
                        string responseData = "";

                        // --- NEW: PARSE THE XML TO EXTRACT ONLY WHAT WE NEED ---
                        try
                        {
                            System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();
                            xmlDoc.LoadXml(soapResult);

                            System.Xml.XmlNodeList codeNodes = xmlDoc.GetElementsByTagName("ResponseCode");
                            if (codeNodes.Count > 0) responseCode = codeNodes[0].InnerText;

                            System.Xml.XmlNodeList descNodes = xmlDoc.GetElementsByTagName("ResponseDescription");
                            if (descNodes.Count > 0) responseDesc = descNodes[0].InnerText;

                            System.Xml.XmlNodeList dataNodes = xmlDoc.GetElementsByTagName("Data");
                            if (dataNodes.Count > 0) responseData = dataNodes[0].InnerText;
                        }
                        catch (Exception xmlEx)
                        {
                            CreateLog($"[XML Parse Error]: {xmlEx.Message} | Raw: {soapResult}", "BAFL_LOG", AppConfig.LogPath);
                        }

                        // Format a clean message
                        string cleanMessage = $"Code: {responseCode} | Desc: {responseDesc}";
                        if (!string.IsNullOrEmpty(responseData))
                        {
                            cleanMessage += $" | Data: {responseData}";
                        }

                        // 1. SHOW IN UI: Print only the clean message
                        ShowActivity(ActivityType.AddTransactionList, $"BAFL OTP Sent [{mobileNo}]. {cleanMessage}");

                        // 2. LOG TO FILE: Write the clean message instead of the raw XML
                        CreateLog($"[BAFL OTP] Mobile: {mobileNo} | OTP: {otp} | {cleanMessage}", "BAFL_LOG", AppConfig.LogPath);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, string.Format("[SendBAFLOtpToGateway Error]: {0}", ex.Message));
                CreateLog(string.Format("[SendBAFLOtpToGateway Error]: {0}", ex.Message), "BAFL_LOG", AppConfig.LogPath);
            }
        }
        
        #region Button and Timer Processes



        private void btnStart_Click(object sender, EventArgs e)
        {
            Configuration _AppConfig = new Configuration();
            
            //StartProcessCCF_AS("923333558358", "test", "123", "UFone", "123", _AppConfig);

            btnStart.Enabled = false;
            btnExit.Enabled = false;
            btnConfiguration.Enabled = false;
            btnStop.Enabled = true;

            if (!string.IsNullOrEmpty(AppConfig.RefreshTime))
            { tmrProcess.Interval = int.Parse(AppConfig.RefreshTime) * 1000; }
            else
            { tmrProcess.Interval = 1000; }

            tmrProcess.Enabled = true;
            tmrUpdateStatus.Enabled = true;
            tmrTime.Enabled = true;
            objStopWatch.Start();

            ShowActivity(ActivityType.AddTransactionList, "Application Started");
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            tmrProcess.Enabled = false;
            tmrUpdateStatus.Enabled = false;
            tmrTime.Enabled = false;

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnConfiguration.Enabled = true;
            btnExit.Enabled = true;
            objStopWatch.Stop();
            ShowActivity(ActivityType.AddTransactionList, "Application Stopped.");

        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            DialogResult a = MessageBox.Show("Are you sure you wish to exit?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (a == DialogResult.Yes)
                this.Close();
            else return;
        }

        private void btnConfiguration_Click(object sender, EventArgs e)
        {
            frmConfig objConfig = new frmConfig();
            objConfig.ShowDialog();
            objConfig = null;

            AppConfig = new Configuration();
            this.Text = AppConfig.EngineName;
            lblAppName.Text = AppConfig.EngineName;
        }

        private void tmrTime_Tick(object sender, EventArgs e)
        {
            tbTimeElapsed.Text = objStopWatch.Elapsed.ToString().Remove(8);
        }

        private void tmrUpdateStatus_Tick(object sender, EventArgs e)
        {
            DAL objDal = new DAL();
            try
            {
                objDal.doExecute("Exec sp_Mon_AppStatus '" + AppConfig.EngineName + "'", AppConfig.MonConnectionString);
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, ex.Message);
                CreateLog(ex.Message, "DeloCampaign", AppConfig.LogPath);
            }
        }
        #endregion

        #region Activity

        private Boolean IsNumeric(String value)
        {
            return value.All(Char.IsDigit);
        }

        private enum ActivityType
        {
            AddTransactionList,
            AddErrirList,
            AddTransaction
        }

        delegate void SetListBox(ListBox _ListBoxe, string Data);
        delegate void SetTextBox(TextBox objTextBox);

        private void ShowActivity(ActivityType _ActivityType, string Data)
        {
            if (string.IsNullOrEmpty(Data)) return;

            if (_ActivityType == ActivityType.AddTransactionList)
            {
                if (lbTransactions.InvokeRequired)
                {
                    SetListBox d = new SetListBox(AddItem);
                    this.Invoke(d, new object[] { lbTransactions, Data });
                }
                else
                {
                    AddItem(lbTransactions, Data);
                }
            }
            else if (_ActivityType == ActivityType.AddErrirList)
            {
                if (lbError.InvokeRequired)
                {
                    SetListBox d = new SetListBox(AddItem);
                    this.Invoke(d, new object[] { lbError, Data });
                }
                else
                {
                    AddItem(lbError, Data);
                }
            }
            else if (_ActivityType == ActivityType.AddTransaction)
            {
                if (tbTransactions.InvokeRequired)
                {
                    SetTextBox t = new SetTextBox(UpdateCount);
                    this.Invoke(t, new object[] { tbTransactions });
                }
                else
                {
                    UpdateCount(tbTransactions);
                }
            }
        }

        private void AddItem(ListBox objListBox, string Data)
        {
            if (objListBox.Items.Count > 100)
            { objListBox.Items.Clear(); }

            objListBox.Items.Add(string.Format("{0} - {1}", DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss"), Data));
            objListBox.SelectedIndex = objListBox.Items.Count - 1;
        }

        private void UpdateCount(TextBox objTextBox)
        {
            double TotalCount = double.Parse(objTextBox.Text) + 1;
            objTextBox.Text = TotalCount.ToString();
            objTextBox.Refresh();
        }

        public void CreateLog(string LogData, string sService, string sLogPath)
        {
            string FileName = sService + "_" + DateTime.Now.ToString("yyyyMMMdd") + ".txt";
            try
            {
                if (!Directory.Exists(sLogPath))
                {
                    Directory.CreateDirectory(sLogPath);
                }
                if (!File.Exists(sLogPath + FileName))
                {
                    StreamWriter swc =  File.CreateText(sLogPath + FileName);
                    swc.Close();
                    swc.Dispose();
                }
                StreamWriter sw = File.AppendText(sLogPath + FileName);
                sw.WriteLine(DateTime.Now.ToString() + "|" + LogData);
                sw.Close();
                sw.Dispose();
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, ex.Message);
            }
        }

        private void lbTransactions_DoubleClick(object sender, EventArgs e)
        {

            if (!string.IsNullOrEmpty(lbTransactions.SelectedItem.ToString()))
            {
                frmDescription objFrmDescription = new frmDescription();
                objFrmDescription.tbDescription.Text = lbTransactions.SelectedItem.ToString();
                objFrmDescription.tbDescription.ForeColor = Color.Black;
                objFrmDescription.ShowDialog();

            }
        }

        private void lbError_DoubleClick(object sender, EventArgs e)
        {

            if (!string.IsNullOrEmpty(lbError.SelectedItem.ToString()))
            {
                frmDescription objFrmDescription = new frmDescription();
                objFrmDescription.tbDescription.Text = lbError.SelectedItem.ToString();
                objFrmDescription.tbDescription.ForeColor = Color.Red;
                objFrmDescription.ShowDialog();

            }
        }

        #endregion Activity



    }
}
