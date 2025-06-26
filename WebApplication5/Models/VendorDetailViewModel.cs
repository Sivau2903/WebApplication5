using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication5.Models
{
    public class VendorDetailViewModel
    {
       
            public string VendorName { get; set; }
           
            public string EmailID { get; set; }
            public string PhoneNumber { get; set; }
            public List<string> Materials { get; set; } = new List<string>();
            public string GSTNO { get; set; }
            public string PanNumber { get; set; }
            public string Address { get; set; }
            public int UniversityID { get; set; }
        

    }
}