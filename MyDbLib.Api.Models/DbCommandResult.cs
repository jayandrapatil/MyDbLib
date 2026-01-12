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
    }
}
