using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDbLib.Api.Models
{
    public sealed class DbCommandResult
    {
        public bool Success { get; set; }
        public int AffectedRecords { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }

        // Success factory
        public static DbCommandResult Ok(int affected)
        {
            return new DbCommandResult
            {
                Success = true,
                AffectedRecords = affected
            };
        }

        // Failure factory
        public static DbCommandResult Fail(string code, string message)
        {
            return new DbCommandResult
            {
                Success = false,
                ErrorCode = code,
                ErrorMessage = message
            };
        }
    }

}
