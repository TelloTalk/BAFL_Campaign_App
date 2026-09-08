using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BAFL_Campaign_App
{
    class Configuration
    {
        private string _EngineName = string.Empty;
        private string _AppConnectionString = string.Empty;
        private string _AppConnectionString2 = string.Empty;
        private string _MonConnectionString = string.Empty;
        private string _RefreshTime = string.Empty;
        private string _TopRecord = string.Empty;
        private string _LogPath = string.Empty;
        private string _InvalidMsg8287 = string.Empty;

        // Pull Service Config
        private string _PullPR = "Y";
        private string _PullDR = "N";
        private string _PullPR_URL = string.Empty;
        private string _PullDR_URL = string.Empty;

        // SendOTAC Service Config
        private string _SENDOTACPR = "Y";
        private string _SENDOTACDR = "N";
        private string _SENDOTACPR_URL = string.Empty;
        private string _SENDOTACDR_URL = string.Empty;

        public Configuration()
        {
            string ConfigFile = Application.StartupPath + "\\" + "Config.xml";
            DataSet dsConfig = new DataSet(); 
            if (System.IO.File.Exists(ConfigFile))
            {
                dsConfig.ReadXml(ConfigFile);
            }
            
            if (dsConfig.Tables.Contains("EngineName") && 
                dsConfig.Tables.Contains("AppConnectionString") &&
                dsConfig.Tables.Contains("AppConnectionString2") &&
                dsConfig.Tables.Contains("MonConnectionString") &&
                dsConfig.Tables.Contains("RefreshTime") &&
                dsConfig.Tables.Contains("TopRecord") &&
                dsConfig.Tables.Contains("LogPath") &&
                dsConfig.Tables.Contains("InvalidMsg8287"))

            {
                _EngineName = dsConfig.Tables["EngineName"].Rows[0]["Value"] as string;
                _AppConnectionString = dsConfig.Tables["AppConnectionString"].Rows[0]["Value"] as string;
                _AppConnectionString2 = dsConfig.Tables["AppConnectionString2"].Rows[0]["Value"] as string;
                _MonConnectionString = dsConfig.Tables["MonConnectionString"].Rows[0]["Value"] as string;
                _RefreshTime = dsConfig.Tables["RefreshTime"].Rows[0]["Value"] as string;
                _TopRecord = dsConfig.Tables["TopRecord"].Rows[0]["Value"] as string;
                _LogPath = dsConfig.Tables["LogPath"].Rows[0]["Value"] as string;
                _InvalidMsg8287 = dsConfig.Tables["InvalidMsg8287"].Rows[0]["Value"] as string;

            }
            // Read Pull Service configurations
            if (dsConfig.Tables.Contains("PullPR")) _PullPR = dsConfig.Tables["PullPR"].Rows[0]["Value"] as string;
            if (dsConfig.Tables.Contains("PullDR")) _PullDR = dsConfig.Tables["PullDR"].Rows[0]["Value"] as string;
            if (dsConfig.Tables.Contains("PullPR_URL")) _PullPR_URL = dsConfig.Tables["PullPR_URL"].Rows[0]["Value"] as string;
            if (dsConfig.Tables.Contains("PullDR_URL")) _PullDR_URL = dsConfig.Tables["PullDR_URL"].Rows[0]["Value"] as string;

            // Read SendOTAC Service configurations
            if (dsConfig.Tables.Contains("SENDOTACPR")) _SENDOTACPR = dsConfig.Tables["SENDOTACPR"].Rows[0]["Value"] as string;
            if (dsConfig.Tables.Contains("SENDOTACDR")) _SENDOTACDR = dsConfig.Tables["SENDOTACDR"].Rows[0]["Value"] as string;
            if (dsConfig.Tables.Contains("SENDOTACPR_URL")) _SENDOTACPR_URL = dsConfig.Tables["SENDOTACPR_URL"].Rows[0]["Value"] as string;
            if (dsConfig.Tables.Contains("SENDOTACDR_URL")) _SENDOTACDR_URL = dsConfig.Tables["SENDOTACDR_URL"].Rows[0]["Value"] as string;
        }

        public string EngineName {get { return _EngineName; }}
        public string AppConnectionString { get { return _AppConnectionString; } }
        public string AppConnectionString2 { get { return _AppConnectionString2; } }
        public string MonConnectionString { get { return _MonConnectionString; } }
        public string RefreshTime { get { return _RefreshTime; } }
        public string TopRecord { get { return _TopRecord; } }
        public string LogPath { get { return _LogPath; } }
        public string InvalidMsg8287 { get { return _InvalidMsg8287; } }


        public string PullServiceUrl
        {
            get
            {
                string pr = (_PullPR ?? "").Trim().ToUpper();
                string dr = (_PullDR ?? "").Trim().ToUpper();

                if (pr == dr)
                {
                    throw new InvalidOperationException($"Invalid Config for PullService: Both PullPR ({pr}) and PullDR ({dr}) cannot be equal. Set one to 'Y' and the other to 'N'.");
                }

                if (pr == "Y") return _PullPR_URL;
                if (dr == "Y") return _PullDR_URL;

                throw new InvalidOperationException("Invalid Config for PullService: Neither PullPR nor PullDR is set to 'Y'.");
            }
        }
        public string SendOtacServiceUrl
        {
            get
            {
                string pr = (_SENDOTACPR ?? "").Trim().ToUpper();
                string dr = (_SENDOTACDR ?? "").Trim().ToUpper();

                if (pr == dr)
                {
                    throw new InvalidOperationException($"Invalid Config for SendOTAC: Both SENDOTACPR ({pr}) and SENDOTACDR ({dr}) cannot be equal. Set one to 'Y' and the other to 'N'.");
                }

                if (pr == "Y") return _SENDOTACPR_URL;
                if (dr == "Y") return _SENDOTACDR_URL;

                throw new InvalidOperationException("Invalid Config for SendOTAC: Neither SENDOTACPR nor SENDOTACDR is set to 'Y'.");
            }
        }
    }
}
