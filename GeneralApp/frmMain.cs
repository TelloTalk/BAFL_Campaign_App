using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

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
                        " from BAFLDB..tblIncoming";
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

                    if (_SCode == "8287")
                    {
                        string otp = _Msg.Replace(" ", "");

                        // 1st Service: Message is purely numbers -> Send OTP to Gateway
                        if (!string.IsNullOrEmpty(otp) && IsNumeric(otp) && (otp.Length == 4 || otp.Length == 8))
                            {
                            SendBAFLOtpToGateway(_Mobile, otp, _Telco, _Msg);
                        }
                        else if (_Msg.ToUpper().Replace(" ", "") == "MNP")
                        {
                            StartProcessMNP(_Mobile, _Msg, _MsgId, _Telco, _SCode, _AppConfig);
                        }

                        // 2nd Service: Non-numeric message -> Process prefix inquiry (BAPULL logic without prefix requirement)
                        else
                        {
                            string fullMessage = _Msg.Trim().ToLower();

                            // Order prefixes from LONGEST to SHORTEST
                            string[] prefixes = new string[] {
                                "internet off", "internet on", "alfa block", "dc block",
                                "cchelp", "ccbpr", "ccbps", "raast", "orbits",
                                "ccms", "bpr", "bps", "ccp", "chq",
                                "help", "more", "ab", "cc", "cu", "ms", "ad"
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

                            if (!string.IsNullOrEmpty(matchedActivity))
                            {
                                matchedAccount = string.Empty;
                                int prIndex = fullMessage.IndexOf(' ');

                                if (prIndex != -1)
                                {
                                    matchedAccount = fullMessage.Substring(prIndex + 1);    // "15 09 2017"
                                }

                                string staticResponse = GetStaticResponseFromDb(matchedActivity);

                                if (!string.IsNullOrEmpty(staticResponse))
                                {
                                    string staticCode = "STATIC_00";

                                    ShowActivity(ActivityType.AddTransactionList, $"BAPULL Static Handled [{_Mobile}]. Response for prefix: {matchedActivity}");
                                    CreateLog($"[BAPULL STATIC] Mobile: {_Mobile} | Activity: {matchedActivity} | Queued MT: {staticResponse}", "BAPULL_LOG", AppConfig.LogPath);

                                    int smsPage = GetSmsPageCount(staticResponse); // Defined here before log calls

                                    InsertMTMessage(_MsgId, _Mobile, _Msg, staticResponse, _Telco, _SCode);
                                    InsertBapullDbLog(_Mobile, _MsgId, fullMessage, matchedActivity, staticCode, staticResponse, _Telco, _SCode, smsPage);
                                }
                                else
                                {
                                    string _acount = matchedAccount;
                                    string _field1 = string.Empty;
                                    string _field2 = string.Empty;

                                    if (matchedActivity == "cu")
                                    {
                                        int cuIndex = matchedAccount.IndexOf(' ');

                                        if (cuIndex != -1)
                                        {
                                            _acount = matchedAccount.Substring(0, cuIndex);      // "4210130414633"
                                            _field1 = matchedAccount.Substring(cuIndex + 1);    // "15 09 2017"
                                            _field1 = _field1.Replace(" ", "/"); // Remove spaces from field1
                                        }
                                    }
                                    else if (matchedActivity == "ccbpr")
                                    {
                                        string[] parts = matchedAccount.Split(' ');
                                        if (parts.Length == 3)
                                        {
                                            _field1 = parts[0];              // "u"
                                            _field2 = parts[1];      // 200
                                            _acount = parts[2];      // 6368
                                        }
                                    }
                                    else if (matchedActivity == "bpr")
                                    {
                                        string[] parts = matchedAccount.Split(' ');
                                        if (parts.Length == 3)
                                        {
                                            _field1 = parts[0];              // "u"
                                            _field2 = parts[1];      // 200
                                            _acount = parts[2];      // 6368
                                        }
                                    }
                                    else if (matchedActivity == "bps")
                                    {
                                        string[] parts = matchedAccount.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                                        _acount = null; // Account number is set to null for BPS

                                        if (parts.Length >= 2)
                                        {
                                            _field1 = parts[0]; // First word after activity
                                            _field2 = parts[1]; // Second word after activity
                                        }
                                    }
                                    else if (matchedActivity == "ccbps")
                                    {
                                        string[] parts = matchedAccount.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                                        _acount = null; // Account number is set to null for BPS

                                        if (parts.Length >= 2)
                                        {
                                            _field1 = parts[0]; // First word after activity
                                            _field2 = parts[1]; // Second word after activity
                                        }
                                    }
                                    else if (matchedActivity == "chq")
                                    {
                                        int cuIndex = matchedAccount.IndexOf(' ');

                                        if (cuIndex != -1)
                                        {
                                            _field1 = matchedAccount.Substring(0, cuIndex);      // "4210130414633"
                                            _acount = matchedAccount.Substring(cuIndex + 1);    // "15 09 2017"
                                        }
                                    }
                                    else if (matchedActivity == "internet off")
                                    {
                                        matchedActivity = "internet";
                                        _acount = matchedAccount.Replace("off", "").Trim();
                                        _field1 = "off";
                                    }
                                    else if (matchedActivity == "internet on")
                                    {
                                        matchedActivity = "internet";
                                        _acount = matchedAccount.Replace("on", "").Trim();
                                        _field1 = "on";
                                    }

                                    // Proceed with normal API gateway call
                                    SendBapullToGateway(_Mobile, matchedActivity, _acount, _field1, _field2, _MsgId, fullMessage, _Telco, _SCode);
                                }
                            }

                            else
                            {
                                StartProcessBAFL_Campaign(_Mobile, _Msg, _MsgId, _Telco, _SCode, _AppConfig);
                            }
                        }
                    }
                    else if (_SCode == "8645")
                    {
                        if (_Msg.ToUpper().Replace(" ", "") == "MNP")
                        {
                            StartProcessMNP(_Mobile, _Msg, _MsgId, _Telco, _SCode, _AppConfig);
                        }
                        else
                        {
                            StartProcessBAFL_Campaign(_Mobile, _Msg, _MsgId, _Telco, _SCode, _AppConfig);
                        }
                    }

                        if (_sResponse == "0")
                    {
                        Sql = "Delete from BAFLDB..tblIncoming WHERE ID = '" + _ID + "' ";
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


        private void StartProcessMNP(string _Mobile, string _Msg, string _MsgId, string _Telco, string _SCode, Configuration _AppConfig)
        {
            string Sql;
            DAL objDAL = new DAL();
            DataTable dt;

            string xTelco = string.Empty;
            string SendMsg = string.Empty;
            string _Mask = _SCode;
            string _MtTable = string.Empty;


            try
            {
                _Msg = _Msg.Replace(" ", "");

                if (_Msg.ToUpper() == "MNP")
                {
                    Sql = "select telco from TLSMSetup..tblPortNumber where Mobile = '" + _Mobile + "' ";
                    dt = objDAL.doSelect(Sql, _AppConfig.MonConnectionString);

                    if (dt.Rows.Count > 0)
                    {
                        xTelco = dt.Rows[0]["telco"].ToString();
                    }
                    else
                    {
                        xTelco = _Telco;
                    }

                    if (xTelco.ToUpper() != _Telco.ToUpper())
                    {
                        Sql = "update TLSMSetup..tblPortNumber set telco = '" + _Telco + "' where Mobile = '" + _Mobile + "' ";
                        objDAL.doExecute(Sql, _AppConfig.MonConnectionString);
                    }

                    //SendMsg = "Thank you for your message.\nDate: " + DateTime.Now.ToString("dd-MMM-yyyy").ToUpper() + "\nTime: " + DateTime.Now.ToString("HH:mm:ss").ToUpper() + "\nYour Operator: " + _Telco;

                    //Sql = " insert into MSG_DB..Campaign_MNP (aDate, SCode, Telco, Mobile, Message, Keyword, SMS, pTelco, cTelco) values " +
                    //    "(getdate(), '" + _SCode + "', '" + _Telco + "','" + _Mobile + "','" + _Msg + "','MNP', '" + SendMsg + "','" + xTelco + "','" + _Telco + "') ";
                    //objDAL.doExecute(Sql, _AppConfig.MonConnectionString);

                    SendMsg = "Thank you for your message.\nDate: " + DateTime.Now.ToString("dd-MMM-yyyy").ToUpper() + "\nTime: " + DateTime.Now.ToString("HH:mm:ss").ToUpper() + "\nYour Operator: " + _Telco;

                    // Escape single quotes for SQL inline queries
                    string safeSendMsg = SendMsg.Replace("'", "''");
                    string safeMsg = _Msg.Replace("'", "''");

                    Sql = " insert into MSG_DB..Campaign_MNP (aDate, SCode, Telco, Mobile, Message, Keyword, SMS, pTelco, cTelco) values " +
                        "(getdate(), '" + _SCode + "', '" + _Telco + "','" + _Mobile + "','" + safeMsg + "','MNP', '" + safeSendMsg + "','" + xTelco + "','" + _Telco + "') ";
                    objDAL.doExecute(Sql, _AppConfig.MonConnectionString);

                    Sql = " exec sp_SendMOMT  '" + _MsgId + "', '" + _Mobile + "', '" + _Msg + "', '" + SendMsg + "', '10', '" + _SCode + "', '" + _Mask + "', '" + _MtTable + "', '" + _Telco + "' ";
                    objDAL.doExecute(Sql, _AppConfig.AppConnectionString);

                    ShowActivity(ActivityType.AddTransactionList, string.Format("[StartProcessMNP]: Reply: {0}", SendMsg));
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, string.Format("[StartProcessMNP]: {0}", ex.Message));
                CreateLog(string.Format("[StartProcessMNP]: {0}", ex.Message), "BAFLSMSMNP", AppConfig.LogPath);
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

        private void SendBapullToGateway(string mobileNo, string activity, string accountData, string field1, string field2, string msgId, string originalMsg, string telco, string shortCode)
        {
            int smsPage = 0; // Declared outside try/catch so it's in scope everywhere

            try
            {
                
                string soapEndpoint = AppConfig.PullServiceUrl;
                string safeOriginalMsg = System.Security.SecurityElement.Escape(originalMsg ?? string.Empty);

                string soapEnvelope = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:pul=""http://PullSMSService"">
                   <soapenv:Header/>
                   <soapenv:Body>
                      <pul:BAFInquiry>
                         <AccountNumber>{accountData}</AccountNumber>
                         <MobileNumber>{mobileNo}</MobileNumber>
                         <Activity>{activity}</Activity>
                         <Field1>{field1}</Field1>
                         <Field2>{field2}</Field2>
                      </pul:BAFInquiry>
                   </soapenv:Body>
                </soapenv:Envelope>";

                //CreateLog($"[BAPULL REQUEST XML] Mobile: {mobileNo} | Request Payload:\n{soapEnvelope}", "BAPULL_LOG", AppConfig.LogPath);


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

                        if (!string.IsNullOrEmpty(gatewayMessage))
                        {
                            gatewayMessage = gatewayMessage.Replace("<", "").Replace(">", "").Replace("'", "").Replace("\"", "").Replace("`", "").Replace("`", "").Trim();
                        }

                        smsPage = GetSmsPageCount(gatewayMessage);

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

                        InsertBapullDbLog(mobileNo, msgId, originalMsg, activity, gatewayCode, gatewayMessage, telco, shortCode, smsPage);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, string.Format("[SendBapullToGateway Error]: {0}", ex.Message));
                CreateLog(string.Format("[SendBapullToGateway Error]: {0}", ex.Message), "BAPULL_LOG", AppConfig.LogPath);

                // --- NEW: LOG EXCEPTIONS TO THE DATABASE TOO ---
                InsertBapullDbLog(mobileNo, msgId, originalMsg, activity, "ERR", $"ERROR: {ex.Message}", telco, shortCode, smsPage);
            }
        }

        private int GetSmsPageCount(string message)
        {
            if (string.IsNullOrEmpty(message))
                return 0;

            int length = message.Length;

            if (length <= 160)
                return 1;

            return (int)Math.Ceiling((double)length / 153);
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

        private void InsertOtpDbLog(string mobile, string telco, string userMessage, string responseCode, string responseDesc)
        {
            try
            {
                using (System.Data.SqlClient.SqlConnection conn = new System.Data.SqlClient.SqlConnection(AppConfig.AppConnectionString))
                {
                    string sql = @"INSERT INTO BAFLDB..tblOtp_Logs 
                           (Mobile, Telco, LogDate, UserMessage, ResponseCode, ResponseDesc) 
                           VALUES 
                           (@Mobile, @Telco, GETDATE(), @UserMessage, @ResponseCode, @ResponseDesc)";

                    using (System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Mobile", mobile ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Telco", telco ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserMessage", userMessage ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ResponseCode", responseCode ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ResponseDesc", responseDesc ?? (object)DBNull.Value);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                CreateLog($"[OTP DB LOG ERROR]: {ex.Message} | Mobile: {mobile}", "BAFL_LOG", AppConfig.LogPath);
            }
        }

        private void InsertBapullDbLog(string mobileNo, string msgId, string userMessage, string activityMatched, string gatewayCode, string extractedMessage, string telco, string shortCode, int smsPage)
        {
            try
            {
                using (System.Data.SqlClient.SqlConnection conn = new System.Data.SqlClient.SqlConnection(AppConfig.AppConnectionString))
                {
                    // Removed GatewayResponse, added GatewayCode
                    string sql = @"INSERT INTO BAFLDB..tblBapull_Logs 
                           (MobileNo, MsgID, UserMessage, Activity, GatewayCode, ResponceMessage, Telco, ShortCode, SMSPage) 
                           VALUES 
                           (@MobileNo, @MsgID, @UserMessage, @Activity, @GatewayCode, @ResponceMessage, @Telco, @ShortCode, @SMSPage)";

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
                        cmd.Parameters.AddWithValue("@SMSPage", smsPage);

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
        private void SendBAFLOtpToGateway(string mobileNo, string otp, string telco, string originalMsg)
        {
            try
            {
                // IMPORTANT: Replace XXX with your actual IP and Port from the documentation
                //string soapEndpoint = "http://192.168.186.75:7800/TwoWaySMS/TwoWaySMS.asmx";UAT
                //        string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                //<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                //  <soap:Body>
                //    <SendOTACBacktoGateway xmlns=""http://tempuri.org/"">
                //      <Message>{otp}</Message>
                //      <MobileNo>{mobileNo}</MobileNo>
                //    </SendOTACBacktoGateway>
                //  </soap:Body>
                //</soap:Envelope>";

                //string soapEndpoint = "http://192.168.186.84/TwoWaySMSService/TwoWaySMS.asmx?op=SendOTACBacktoGateway";
                string soapEndpoint = AppConfig.SendOtacServiceUrl;
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
                        InsertOtpDbLog(mobileNo, telco, originalMsg, responseCode, responseDesc);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, string.Format("[SendBAFLOtpToGateway Error]: {0}", ex.Message));
                CreateLog(string.Format("[SendBAFLOtpToGateway Error]: {0}", ex.Message), "BAFL_LOG", AppConfig.LogPath);
                InsertOtpDbLog(mobileNo, telco, originalMsg, "ERR", $"ERROR: {ex.Message}");
            }
        }

        private void StartProcessBAFL_Campaign(string _Mobile, string _Msg, string _MsgId, string _Telco, string _SCode, Configuration _AppConfig)
        {
            DAL objDAL = new DAL();
            DataTable dt;

            string _MtTable = string.Empty;
            string CampaignKeyword = string.Empty;
            string OtherText = string.Empty;
            string ResponseMessage = string.Empty;
            string Status = string.Empty;

            SqlCommand sCommand = new SqlCommand();

            try
            {
                if (_Msg.IndexOf(" ") > 0)
                {
                    CampaignKeyword = _Msg.Substring(0, _Msg.IndexOf(" "));
                    OtherText = _Msg.Substring(_Msg.IndexOf(" ") + 1);
                }
                else
                {
                    CampaignKeyword = _Msg;
                    OtherText = string.Empty;
                }

                string responseCode = string.Empty;

                _Msg = _Msg.Replace("'", " ");

                sCommand.CommandType = CommandType.StoredProcedure;
                    sCommand.CommandText = "TelloCast..sp_setup2WayCampaign";
                    sCommand.Parameters.Clear();

                    sCommand.Parameters.Add(new SqlParameter("@Type", "ProcessCampaignMessage"));
                    sCommand.Parameters.Add(new SqlParameter("@SCode", _SCode));
                    sCommand.Parameters.Add(new SqlParameter("@Telco", _Telco));
                    sCommand.Parameters.Add(new SqlParameter("@Mobile", _Mobile));
                    sCommand.Parameters.Add(new SqlParameter("@Message", _Msg));
                    sCommand.Parameters.Add(new SqlParameter("@campaignKeyword", CampaignKeyword));
                    sCommand.Parameters.Add(new SqlParameter("@OtherText", OtherText));

                    dt = objDAL.doSelectSQLCommand(sCommand, _AppConfig.AppConnectionString2);


                if (dt.Rows.Count > 0)
                {
                    ShowActivity(ActivityType.AddTransactionList, string.Format("ResponseMessage from Campaign : {0}", dt.Rows[0]["ResponseMessage"].ToString()));
                    ShowActivity(ActivityType.AddTransactionList, string.Format("Status  : {0}", dt.Rows[0]["Status"].ToString()));
                    ResponseMessage = dt.Rows[0]["ResponseMessage"].ToString();
                    Status = dt.Rows[0]["Status"].ToString();

                    switch (_Telco.ToLower())
                    {
                        case "mobilink":
                            _MtTable = "tblmt_Mobilink";
                            break;
                        case "ufone":
                            _MtTable = "tblmt_UFone";
                            break;
                        case "telenor":
                            _MtTable = "tblmt_Telenor";
                            break;
                        case "warid":
                            _MtTable = "tblmt_warid";
                            break;
                        case "zong":
                            _MtTable = "tblmt_Zong";
                            break;
                    }

                    if (Status == "INVALID")
                    {

                        ResponseMessage = _AppConfig.InvalidMsg8287;


                    }

                    if (ResponseMessage.Length > 0)
                    {
                        sCommand.CommandType = CommandType.StoredProcedure;
                        sCommand.CommandText = "sp_SendMT";
                        sCommand.Parameters.Clear();

                        sCommand.Parameters.Add(new SqlParameter("@MsgID", _MsgId));
                        sCommand.Parameters.Add(new SqlParameter("@Mobile", _Mobile));
                        sCommand.Parameters.Add(new SqlParameter("@Msg", _Msg));
                        sCommand.Parameters.Add(new SqlParameter("@SMS", ResponseMessage));
                        sCommand.Parameters.Add(new SqlParameter("@SMSType", "10"));
                        sCommand.Parameters.Add(new SqlParameter("@ShortCode", _SCode));
                        sCommand.Parameters.Add(new SqlParameter("@Mask", _SCode));
                        sCommand.Parameters.Add(new SqlParameter("@MTTable", _MtTable));

                        objDAL.doExecuteSQLCommand(sCommand, _AppConfig.AppConnectionString);
                        ShowActivity(ActivityType.AddTransactionList, string.Format("ResponseMessage : {0}", ResponseMessage));
                        ShowActivity(ActivityType.AddTransactionList, string.Format("ResponseMessage sent."));
                    }
                    else
                    {
                        ShowActivity(ActivityType.AddTransactionList, string.Format("ResponseMessage is Empty."));
                    }
                }
            }
            catch (Exception ex)
            {
                ShowActivity(ActivityType.AddErrirList, string.Format("[StartProcessBAFL_Campaign]: {0}", ex.Message));
                CreateLog(string.Format("[StartProcessBAFL_Campaign]: {0}", ex.Message), "HBLSMSMO", AppConfig.LogPath);
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
