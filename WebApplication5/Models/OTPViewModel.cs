﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace WebApplication5.Models
{
    public class OTPViewModel
    {
        [Required]
        public string OTP { get; set; }
    }
}