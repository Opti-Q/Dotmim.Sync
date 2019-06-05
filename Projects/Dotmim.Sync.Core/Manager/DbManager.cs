using System;
using System.Data.Common;
using System.Globalization;
using System.Text;

namespace Dotmim.Sync.Manager
{
    public abstract class DbManager
    {

        public string TableName { get; }

        public DbManager(string tableName)
        {
            this.TableName = tableName;
        }

        /// <summary>
        /// Gets a table manager, who can execute somes queries directly on source database
        /// </summary>
        public abstract IDbManagerTable CreateManagerTable(DbConnection connection, DbTransaction transaction = null);

        /// <summary>
        /// Get a parameter even if it's a @param or :param or param
        /// </summary>
        public static DbParameter GetParameter(DbCommand command, string parameterName)
        {
            if (command == null)
                return null;

            string p1 = $"@{parameterName}";
            string p2 = $":{parameterName}";
            string p3 = $"in{parameterName}";

            for (int i = 0; i < command.Parameters.Count; i++)
            {
                var curr = command.Parameters[i];

                if (curr.ParameterName == p1)
                    return curr;

                if (curr.ParameterName == p2)
                    return curr;

                if (curr.ParameterName == p3)
                    return curr;

                if (curr.ParameterName == parameterName)
                    return curr;
            }

            return null;
        }

        /// <summary>
        /// Set a parameter value
        /// </summary>
        public static void SetParameterValue(DbCommand command, string parameterName, object value)
        {
            DbParameter parameter = GetParameter(command, parameterName);
            if (parameter == null)
                return;

            SetParameterValue(command, parameter, value);
        }

        /// <summary>
        /// Set a parameter value
        /// </summary>
        public static void SetParameterValue(DbCommand command, DbParameter parameter, object value)
        {
            parameter.Value = value == null ? DBNull.Value : value;
        }

        public static int GetSyncIntOutParameter(string parameter, DbCommand command)
        {
            DbParameter dbParameter = GetParameter(command, parameter);
            if (dbParameter == null || dbParameter.Value == null || string.IsNullOrEmpty(dbParameter.Value.ToString()))
                return 0;

            return int.Parse(dbParameter.Value.ToString(), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parse a time stamp value
        /// </summary>
        public static long ParseTimestamp(object obj)
        {
            long timestamp = 0;

            if (obj == DBNull.Value)
                return 0;

            if (obj is long || obj is int || obj is ulong || obj is uint || obj is decimal)
                return Convert.ToInt64(obj, NumberFormatInfo.InvariantInfo);

            string str = obj as string;
            if (str != null)
            {
                long.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out timestamp);
                return timestamp;
            }

            byte[] numArray = obj as byte[];
            if (numArray == null)
                return 0;

            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < numArray.Length; i++)
            {
                string str1 = numArray[i].ToString("X", NumberFormatInfo.InvariantInfo);
                stringBuilder.Append((str1.Length == 1 ? string.Concat("0", str1) : str1));
            }

            long.TryParse(stringBuilder.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out timestamp);
            return timestamp;
        }


 
    }
}
