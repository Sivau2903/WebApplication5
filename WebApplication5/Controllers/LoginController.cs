﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using WebApplication5.Models;

namespace WebApplication5.Controllers
{
    public class LoginController : Controller
    {
        private readonly ASPEntities2 _db = new ASPEntities2();
        // GET: Login

        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Loginpage()
        {
            return View();
        }


        [HttpPost]
        public ActionResult Login(Loginviewmodel model)
        {
            Debug.WriteLine("Login attempt started.");

            if (ModelState.IsValid)
            {
                Debug.WriteLine($"Model is valid. UserID: {model.UserID}, Password: {model.Password}");

                var user = _db.Logins.FirstOrDefault(u => u.UserID == model.UserID && u.PasswordHash == model.Password);

                if (user != null)
                {
                    Debug.WriteLine($"User found: {user.UserID}, Role: {user.Role}");

                    Session["UserID"] = user.UserID;
                    Session["UserRole"] = user.Role;

                    if (user.Role == "Admin")
                    {
                        Debug.WriteLine("Redirecting to Admin Dashboard.");
                        return RedirectToAction("StoreAdminDasboard", "Home");
                    }
                    if (user.Role == "Employee")
                    {
                        Debug.WriteLine("Redirecting to Employee Dashboard.");
                        return RedirectToAction("EmployeeDashboard", "Employee");
                    }
                    if (user.Role == "HOD")
                    {
                        Debug.WriteLine("Redirecting to HOD Dashboard.");
                        return RedirectToAction("Home", "HOD");
                    }
                    if (user.Role == "SuperAdmin")
                    {
                        Debug.WriteLine("Redirecting to HOD Dashboard.");
                        return RedirectToAction("Home", "University");
                    }

                    if (user.Role == "LocalAccountant")
                    {
                        Debug.WriteLine("Redirecting to LocalAccountant Dashboard.");
                        return RedirectToAction("LADashBoard", "LocalAccountant");
                    }

                    if (user.Role == "Local Vendor")
                    {
                        Debug.WriteLine("Redirecting to LocalVendor Dashboard.");
                        return RedirectToAction("LVDashBoard", "LocalVendor");
                    }

                    if (user.Role == "CentralAccountant")
                    {
                        Debug.WriteLine("Redirecting to CentralAccountant Dashboard.");
                        return RedirectToAction("DashBoard", "CentralAccountant");
                    }

                    if (user.Role == "CentralPurchaseD")
                    {
                        Debug.WriteLine("Redirecting to CentralPurchaseD Dashboard.");
                        return RedirectToAction("CPDDashBoard", "CentralPurchaseDepartment");
                    }

                    if (user.Role == "CentralVendor")
                    {
                        Debug.WriteLine("Redirecting to CentralVendor Dashboard.");
                        return RedirectToAction("CVDashBoard", "CentralVendor");
                    }

                    if (user.Role == "localPurchaseD")
                    {
                        Debug.WriteLine("Redirecting to localPurchaseD Dashboard.");
                        return RedirectToAction("LPDDashBoard", "LocalPurchaseDepartment");
                    }

                    if (user.Role == "Auditor")
                    {
                        Debug.WriteLine("Redirecting to Auditor Dashboard.");
                        return RedirectToAction("Home", "Auditor");
                    }

                    if (user.Role == "IUCD")
                    {
                        Debug.WriteLine("Redirecting to Summarizer Dashboard.");
                        return RedirectToAction("Home", "IUCD");
                    }

                    Debug.WriteLine("Access Denied! Invalid Role.");
                    TempData["Message"] = "Access Denied! Invalid Role.";
                }
                else
                {
                    Debug.WriteLine("Invalid UserID or Password.");
                    TempData["ErrorMessage"] = "Invalid email or password.";
                    return View("Loginpage");
                }
            }

            else
            {
                Debug.WriteLine("ModelState is not valid.");
                TempData["warningMessage"] = "Please fill in all required fields correctly.";
            }

            return View();
        }


        //public ActionResult Logout()
        //{
        //    // Clear all session data
        //    Session.Clear();
        //    Session.Abandon();
        //    Session.RemoveAll();

        //    // Remove authentication cookie
        //    if (Request.Cookies[".AspNet.ApplicationCookie"] != null)
        //    {
        //        var cookie = new HttpCookie(".AspNet.ApplicationCookie")
        //        {
        //            Expires = DateTime.Now.AddDays(-1),
        //            Value = null
        //        };
        //        Response.Cookies.Set(cookie);
        //    }

        //    // Disable browser caching to prevent back navigation
        //    Response.Cache.SetCacheability(HttpCacheability.NoCache);
        //    Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
        //    Response.Cache.SetNoStore();

        //    // Redirect to login page
        //    return RedirectToAction("Loginpage", "Login");
        //}

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();

            Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            Response.Cache.SetValidUntilExpires(false);
            Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            return RedirectToAction("LoginPage", "Login");
        }


        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            Response.Cache.SetValidUntilExpires(false);
            Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            base.OnActionExecuting(filterContext);
        }


        [HttpGet]
        public ActionResult MyProfile()
        {
            // 1) Check session
            if (Session["UserID"] == null || Session["UserRole"] == null)
            {
                return RedirectToAction("Loginpage");
            }

            var userIdObj = Session["UserID"];
            var role = Session["UserRole"].ToString();

            // Convert userIdObj to the correct type if needed
            // e.g. int userId = (int)userIdObj; 
            // If your UserID is string in the DB, adjust accordingly
            string userId = (string)Session["UserID"];


            // 2) Prepare a profile view model
            var profileVM = new Profileviewmodel();

            // 3) Depending on role, fetch from correct table
            if (role == "Employee")
            {
                var emp = _db.Employees.FirstOrDefault(e => e.EmpID == userId);
                if (emp != null)
                {
                    profileVM.Name = emp.FirstName + " " + emp.LastName;
                    profileVM.ID = emp.EmpID;
                    profileVM.Email = emp.EmailID;
                    profileVM.PhoneNumber = emp.PhoneNumber;
                    profileVM.Department = emp.DepName;
                    profileVM.Role = "Employee";
                }
            }
            else if (role == "HOD")
            {
                var hod = _db.HODs.FirstOrDefault(h => h.HODID == userId);
                if (hod != null)
                {
                    profileVM.Name = hod.FirstName + " " + hod.LastName;
                    profileVM.ID = hod.HODID;
                    profileVM.Email = hod.EmailID;
                    profileVM.PhoneNumber = hod.PhoneNumber;
                    profileVM.Department = hod.DepName;
                    profileVM.Role = "HOD";
                }
            }
            else if (role == "Admin")
            {
                // Example: If Admin data is stored in StoreAdmins table
                var admin = _db.StoreAdmins.FirstOrDefault(a => a.StoreAdminID.ToString() == userId);
                if (admin != null)
                {
                    profileVM.Name = admin.FirstName + " " + admin.LastName;
                    profileVM.ID = admin.StoreAdminID;
                    profileVM.Email = admin.EmailID;
                    profileVM.PhoneNumber = admin.PhoneNumber;
                    // Admin may not have a department
                    profileVM.Department = "N/A";
                    profileVM.Role = "Admin";
                }
            }

            //else if (role == "SuperAdmin")
            //{
            //    // Example: If Admin data is stored in StoreAdmins table
            //    var superadmin = _db.Universities.FirstOrDefault(a => a.UniversityId.ToString() == userId);
            //    if (superadmin != null)
            //    {
            //        profileVM.Name = superadmin.UniversityName;
            //        profileVM.ID = superadmin.UniversityId;
            //        //profileVM.Email = admin.EmailID;
            //        //profileVM.PhoneNumber = admin.PhoneNumber;
            //        // Admin may not have a department
            //        profileVM.Department = "N/A";
            //        profileVM.Role = "SuperAdmin";
            //    }
            //}
            else if (role == "LocalAccountant")
            {
                var hod = _db.LocalAccountants.FirstOrDefault(h => h.LocalAccountantID.ToString() == userId);
                if (hod != null)
                {
                    profileVM.Name = hod.FirstName + " " + hod.LastName;
                    profileVM.ID = hod.LocalAccountantID;
                    profileVM.Email = hod.EmailID;
                    profileVM.PhoneNumber = hod.PhoneNumber;
                    profileVM.Department = "Accountant";
                    profileVM.Role = "LocalAccountant";
                }
            }

            //else if (role == "LocalVendor")
            //{
            //    var hod = _db.VendorDetails.FirstOrDefault(h => h.VID.ToString() == userId);
            //    if (hod != null)
            //    {
            //        profileVM.Name = hod.FirstName + " " + hod.LastName;
            //        profileVM.ID = hod.VID;
            //        profileVM.Email = hod.EmailID;
            //        profileVM.PhoneNumber = hod.PhoneNumber;
            //        //profileVM.Department = "N/A";
            //        profileVM.Role = "LocalVendor";
            //    }
            //}

            else if (role == "CentralAccountant")
            {
                var hod = _db.CentralAccountants.FirstOrDefault(h => h.CentralAccountantID.ToString() == userId);
                if (hod != null)
                {
                    profileVM.Name = hod.FirstName + " " + hod.LastName;
                    profileVM.ID = hod.CentralAccountantID;
                    profileVM.Email = hod.EmailID;
                    profileVM.PhoneNumber = hod.PhoneNumber;
                    //profileVM.Department = hod.DepName;
                    profileVM.Role = "CentralAccountant";
                }
            }

            else if (role == "CentralPurchaseD")
            {
                var hod = _db.CentralPurchaseDepartments.FirstOrDefault(h => h.CentralID.ToString() == userId);
                if (hod != null)
                {
                    profileVM.Name = hod.CentralDepartmentName;
                    profileVM.ID = hod.CentralID;
                    profileVM.Email = "N/A";
                    profileVM.PhoneNumber = "N/A";
                    profileVM.Department = "N/A";
                    profileVM.Role = "CentralPurchaseDepartment";
                }
            }

            //else if (role == "CentralVendor")
            //{
            //    var hod = _db.CentralVendorDetails.FirstOrDefault(h => h.CVID.ToString() == userId);
            //    if (hod != null)
            //    {
            //        profileVM.Name = hod.FirstName + " " + hod.LastName;
            //        profileVM.ID = hod.CVID;
            //        profileVM.Email = hod.EmailID;
            //        profileVM.PhoneNumber = hod.PhoneNumber;
            //        //profileVM.Department = hod.DepName;
            //        profileVM.Role = "CentralVendor";
            //    }
            //}

            else if (role == "localPurchaseD")
            {
                var hod = _db.LocalPurchaseDepartments.FirstOrDefault(h => h.LocalID.ToString() == userId);
                if (hod != null)
                {
                    //profileVM.Name = hod.;
                    profileVM.ID = hod.LocalID;
                    profileVM.Email = hod.EmailID;
                    //profileVM.PhoneNumber = hod.PhoneNumber;
                    //profileVM.Department = hod.DepName;
                    profileVM.Role = "localPurchaseD";
                }
            }

            else if (role == "Summarizer")
            {
                var hod = _db.IUCD_.FirstOrDefault(h => h.ID.ToString() == userId);

                if (hod != null)
                {
                    profileVM.Name = hod.FirstName + " " + hod.LastName;
                    profileVM.ID = hod.ID.ToString();
                    profileVM.Email = hod.EmailID;
                    profileVM.PhoneNumber = hod.PhoneNumber;
                    //profileVM.Department = "Nill";
                    profileVM.Role = "IUCD Department";
                }
            }

            else if (role == "Auditor")
            {
                var hod = _db.CentralAuditors.FirstOrDefault(h => h.AuditorID == userId);
                if (hod != null)
                {
                    profileVM.Name = hod.FirstName + " " + hod.LastName;
                    profileVM.ID = hod.AuditorID;
                    profileVM.Email = hod.EmailID;
                    profileVM.PhoneNumber = hod.PhoneNumber;
                    //profileVM.Department = "Nill";
                    profileVM.Role = "Auditor";
                }
            }

            // 4) Return partial view with the data
            return PartialView("_MyProfilePartial", profileVM);
        }


        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Please provide a valid email.";
                return View();
            }

            var user = _db.Logins.FirstOrDefault(u => u.EmailID == email);

            if (user == null)
            {
                TempData["ErrorMessage"] = "No account found with this email.";
                return View();
            }

            // Generate OTP
            string otp = new Random().Next(100000, 999999).ToString();

            Session["OTP"] = otp;
            Session["ResetEmail"] = email;

            // Send OTP Email
            try
            {
                using (var message = new MailMessage())
                {
                    message.From = new MailAddress("shivaupputuri5@gmail.com");
                    message.To.Add(email);
                    message.Subject = $"Password Reset OTP – {otp}";
                    message.Body = $"Your OTP for password reset is {otp}.";

                    using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                    {
                        smtp.Credentials = new NetworkCredential("shivaupputuri5@gmail.com", "uxwvtphmvzhqqqpl");
                        smtp.EnableSsl = true;
                        smtp.Send(message);
                    }
                }

                TempData["Message"] = "OTP sent successfully.";
                return RedirectToAction("VerifyOTP");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Failed to send OTP.";
                return View();
            }
        }

        // Get OTP Verification page
        [HttpGet]
        public ActionResult VerifyOTP()
        {
            return View();
        }

        // Post OTP Verification
       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyOTP(string otp)
        {
            if (Session["OTP"] == null || Session["ResetEmail"] == null)
            {
                TempData["ErrorMessage"] = "Session expired. Please try again.";
                return RedirectToAction("ForgotPassword");
            }

            if (otp == Session["OTP"].ToString())
            {
                // OTP verified
                return RedirectToAction("ResetPassword");
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid OTP. Please try again.";
                return View();
            }
        }


        [HttpGet]
        public ActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string oldPassword, string newPassword, string confirmPassword)
        {
            string email = Session["ResetEmail"]?.ToString();
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Session expired. Please try again.";
                return RedirectToAction("ForgotPassword");
            }

            var user = _db.Logins.FirstOrDefault(u => u.EmailID == email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ForgotPassword");
            }

            if (user.PasswordHash != oldPassword)
            {
                TempData["ErrorMessage"] = "Old password is incorrect.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "New Password and Confirm Password do not match.";
                return View();
            }

            user.PasswordHash = newPassword;
            _db.SaveChanges();

            TempData["Msg"] = "Password reset successfully. Please login with your new password.";
            return RedirectToAction("LoginPage");
        }
    }


    }
