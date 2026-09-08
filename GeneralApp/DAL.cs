using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BAFL_Campaign_App
{
    class DAL
    {
        public DataTable doSelect(string strQry, string StrConnectionString)
        {
            SqlConnection conn = new SqlConnection(StrConnectionString);
            SqlDataAdapter da = new SqlDataAdapter(strQry, conn);
            DataTable dt = new DataTable();
            try
            {
                conn.Open();
                da.Fill(dt);
                conn.Close();
                return dt;
            }
            catch (Exception ex)
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
                throw new Exception(ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
            }
        }

        public void doExecute(string strQry, string StrConnectionString)
        {
            SqlConnection conn = new SqlConnection(StrConnectionString);
            SqlCommand cmd = new SqlCommand(strQry, conn);
            try
            {
                conn.Open();
                cmd.ExecuteNonQuery();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
                throw new Exception(ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
            }

        }

        public string doExecuteScalar(string strQry, string StrConnectionString)
        {
            string res;
            SqlConnection conn = new SqlConnection(StrConnectionString);
            SqlCommand cmd = new SqlCommand(strQry, conn);

            try
            {
                conn.Open();
                res = Convert.ToString(cmd.ExecuteScalar());
                conn.Close();
                return res;
            }
            catch (Exception ex)
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
                throw new Exception(ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
            }

        }

        public DataTable doSelectSQLCommand(SqlCommand sCommand, string StrConnectionString)
        {
            SqlConnection conn = new SqlConnection(StrConnectionString);
            SqlDataAdapter da = new SqlDataAdapter(sCommand);
            da.SelectCommand.CommandTimeout = 30;
            DataTable dt = new DataTable();
            try
            {
                sCommand.Connection = conn;
                conn.Open();
                da.Fill(dt);
                conn.Close();
                return dt;
            }
            catch (Exception ex)
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
                throw new Exception(ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
            }
        }
        public void doExecuteSQLCommand(SqlCommand sCommand, string StrConnectionString)
        {
            SqlConnection conn = new SqlConnection(StrConnectionString);
            try
            {
                sCommand.Connection = conn;
                conn.Open();
                sCommand.ExecuteNonQuery();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
                throw new Exception(ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
            }
        }
        public string doExecuteScalarSQLCommand(SqlCommand sCommand, string StrConnectionString)
        {
            string res;
            SqlConnection conn = new SqlConnection(StrConnectionString);
            try
            {
                sCommand.Connection = conn;
                conn.Open();
                res = Convert.ToString(sCommand.ExecuteScalar());
                conn.Close();
                return res;
            }
            catch (Exception ex)
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
                throw new Exception(ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open) { conn.Close(); }
            }

        }
    }
}
