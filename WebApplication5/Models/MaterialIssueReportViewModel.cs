﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication5.Models
{
    public class MaterialIssueReportViewModel
    {
        public string MaterialName { get; set; }
        public DateTime IssuedDate { get; set; }
        public int TotalIssued { get; set; }
        public int OpeningQuantity { get; set; }
        public int ClosingQuantity { get; set; }
    }
}