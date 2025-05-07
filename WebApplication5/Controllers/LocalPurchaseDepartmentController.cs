using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using Newtonsoft.Json;

using System.Web;
using System.Web.Mvc;
using WebApplication4.Models;
using WebApplication5.Models;
using System.Data.Entity.Validation;

namespace WebApplication5.Controllers
{
    public class LocalPurchaseDepartmentController : BaseController
    {
        private readonly ASPEntities _db = new ASPEntities();
        private readonly EmailService _emailService = new EmailService();  // Injecting EmailService
        // GET: LocalPurchaseDepartment
        public ActionResult LPDDashBoard()
        {
            return View();
        }

        public ActionResult Materials()
        {
            var materials = _db.MaterialMasterLists.ToList(); // Fetch all materials
            return View(materials);
        }

        public ActionResult RequestsRecevied()
        {
            string purchaseDepartmentID = (string)Session["UserID"];

            var raisedRequests = (from request in _db.TempSelectedMaterials
                                  where request.UserID == purchaseDepartmentID && request.Status == "New"
                                  select request).ToList();



            return View(raisedRequests);
        }

        [HttpPost]
        public ActionResult AddToReceivedRequests(string materialSubCategory)
        {
            string userId = (string)Session["UserID"];
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Session UserID: {userId}");

            if (userId == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] No UserID in session.");
                return PartialView("_VendorDetailsPartial", new List<VendorDetail>());
            }

            // Step 2: Get UniversityID from LocalPurchaseDepartment table based on UserID
            var localPD = _db.LocalPurchaseDepartments.FirstOrDefault(x => x.LocalID == userId);
            if (localPD == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] No LocalPurchaseDepartment found for UserID: {userId}");
                return PartialView("_VendorDetailsPartial", new List<VendorDetail>());
            }

            int universityId = localPD.UniversityID;
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Found UniversityID: {universityId} for UserID: {userId}");

            // Fix for CS0019: Ensure proper type conversion for comparison  
            var exists = _db.TempSelectedMaterials
                           .Any(m => m.UserID == userId &&
                                     m.MaterialSubCategory == materialSubCategory &&
                                     m.Status == "New");

            if (!exists)
            {
                _db.TempSelectedMaterials.Add(new TempSelectedMaterial
                {
                    UserID = (string)Session["UserID"],
                    MaterialSubCategory = materialSubCategory,
                    Status = "New",
                    UniversityID = universityId.ToString()
                });
                _db.SaveChanges();
            }

            return RedirectToAction("Materials");
        }


        public JsonResult GetAvailableBudget()
        {
            string sessionID = (string)Session["UserID"]; // Adjust based on your session handling
            var budget = _db.LocalPurchaseDepartments
                            .Where(lpd => lpd.LocalID == sessionID)
                            .Select(lpd => lpd.Budget)
                            .FirstOrDefault();

            return Json(budget, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult SendToCentral(string materialName, int quantity, HttpPostedFileBase certificateUpload)
        {
            try
            {
                if (certificateUpload == null || certificateUpload.ContentLength == 0)
                {
                    return Json(new { success = false, message = "Certificate file is required." });
                }

                string localUserId = (string)Session["UserID"];

                var user = _db.LocalPurchaseDepartments.FirstOrDefault(e => e.LocalID == localUserId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var fileName = Path.GetFileNameWithoutExtension(certificateUpload.FileName);
                var extension = Path.GetExtension(certificateUpload.FileName);
                fileName = fileName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
                var filePath = Path.Combine(Server.MapPath("~/UploadedCertificates"), fileName);
                certificateUpload.SaveAs(filePath);

                // Save relative path in DB
                filePath = "~/UploadedCertificates/" + fileName;

                //request.PurchaseDepartmentUploads = "/Uploads/" + fileName; // Save only the path

                var request = new SavetoCentral
                {
                    LocalID = localUserId,
                    CentralID = "IURCPD1",
                    UniversityID = user.UniversityID,
                    Material = materialName,
                    Order_Quantity = quantity,
                    RequestedDate = DateTime.Now,
                    PurchaseDepartmentUploads = filePath, // Store the certificate file here
                    Status = "Sent to Central"
                };

                _db.SavetoCentrals.Add(request);

                var tempMaterial = _db.TempSelectedMaterials
                                      .FirstOrDefault(t => t.UserID == localUserId && t.MaterialSubCategory == materialName && t.Status == "New");

                if (tempMaterial != null)
                {
                    tempMaterial.Status = "Sent";
                    _db.Entry(tempMaterial).State = EntityState.Modified;
                }

                _db.SaveChanges();

                return Json(new { success = true, message = "Sent to Central Department." });
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(eve => eve.ValidationErrors)
                    .Select(ve => $"{ve.PropertyName}: {ve.ErrorMessage}");

                var fullErrorMessage = string.Join("; ", errorMessages);
                var exceptionMessage = $"Validation failed: {fullErrorMessage}";

                return Json(new { success = false, message = exceptionMessage });
            }

        }



        public ActionResult GetVendors(string materialsubCategory)
        {
            // Step 1: Get UserID from session                
            string userId = (string)Session["UserID"];
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Session UserID: {userId}");

            if (userId == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] No UserID in session.");
                return PartialView("_VendorDetailsPartial", new List<VendorDetail>());
            }

            // Step 2: Get UniversityID from LocalPurchaseDepartment table based on UserID
            var localPD = _db.LocalPurchaseDepartments.FirstOrDefault(x => x.LocalID == userId);
            if (localPD == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] No LocalPurchaseDepartment found for UserID: {userId}");
                return PartialView("_VendorDetailsPartial", new List<VendorDetail>());
            }

            int universityId = localPD.UniversityID;
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Found UniversityID: {universityId} for UserID: {userId}");

            // Step 3: Fetch vendor details
            var vendorDetails = _db.VendorDetails
                .Where(v => v.UniversityID == universityId &&
                            v.Material.Trim().ToLower() == materialsubCategory.Trim().ToLower())
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[DEBUG] MaterialSubCategory: {materialsubCategory}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Vendor count fetched: {vendorDetails.Count}");

            return PartialView("_VendorDetailsPartial", vendorDetails);
        }


        //public ActionResult GeneratePO(string material, int qty, string vendorId, decimal price, decimal cost, string email)
        //{
        //    // Fetch user info from session
        //    int userId = Convert.ToInt32(Session["UserID"]);
        //    var user = _db.LocalPurchaseDepartments.FirstOrDefault(u => u.LocalID == userId);
        //    if (user == null) return HttpNotFound("User not found");

        //    // Fetch university info
        //    var university = _db.Universities.FirstOrDefault(u => u.UniversityId == user.UniversityID);
        //    if (university == null) return HttpNotFound("University not found");

        //    // Generate unique requisition number
        //    string requisitionNo = "REQ-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 5).ToUpper();

        //    var viewModel = new GeneratePOViewModel
        //    {
        //        //PONumber = "PO-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
        //        VendorEmail = email,

        //        UniversityName = university.UniversityName,
        //        UniversityPhone = university.UniversityPhone,
        //        UniversityEmail = university.UniversityEmail,
        //        UniversityAddress = university.Address,
        //        RequisitionedBy = university.UniversityName,
        //        RequisitionNo = requisitionNo,
        //        AuthorizedBy = university.UniversityName,

        //        PurchaseOrderItems = new List<PurchaseOrderItem>
        //{
        //    new PurchaseOrderItem
        //    {
        //        Description = material,
        //        QtyOrdered = qty,
        //        UnitPrice = price,
        //        Total = cost,
        //        VendorID = Convert.ToInt32(vendorId)
        //    }
        //}
        //    };

        //    ViewBag.University = university;
        //    return View(viewModel);
        //}

        [HttpPost]
        public ActionResult GeneratePO(string vendorId, string email, string materials)
        {
            if (string.IsNullOrEmpty(materials))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Materials data is missing.");
            }

            var materialList = JsonConvert.DeserializeObject<List<MaterialItem>>(materials);

            string userId = (string)Session["UserID"];
            var user = _db.LocalPurchaseDepartments.FirstOrDefault(u => u.LocalID == userId);
            if (user == null) return HttpNotFound("User not found");

            var university = _db.Universities.FirstOrDefault(u => u.UniversityId == user.UniversityID);
            if (university == null) return HttpNotFound("University not found");

            string requisitionNo = "REQ-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 5).ToUpper();

            var viewModel = new GeneratePOViewModel
            {
                VendorEmail = email,
                UniversityName = university.UniversityName,
                UniversityPhone = university.UniversityPhone,
                UniversityEmail = university.UniversityEmail,
                UniversityAddress = university.Address,
                RequisitionedBy = university.UniversityName,
                AuthorizedBy = university.UniversityName,
                RequisitionNo = requisitionNo,
                PurchaseOrderItems = materialList.Select(m => new PurchaseOrderItem
                {
                    Description = m.MaterialName,
                    QtyOrdered = m.QtyOrdered,
                    UnitPrice = m.UnitPrice,
                    Total = m.Total,
                    VendorID = Convert.ToInt32(vendorId),
                    VendorEmail = email
                }).ToList()
            }; 

            ViewBag.University = university;
            return View(viewModel);
        }

        [HttpPost]
        public ActionResult GeneratePO123(GeneratePOViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);

            }

            try
            {
                // Get current logged-in User ID
                string userId = (string)Session["UserID"];

                // Get UniversityID by current User (Assuming mapping exists)
                var user = _db.LocalPurchaseDepartments.FirstOrDefault(u => u.LocalID == userId);
                if (user == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    return View(viewModel);
                }

                var university = _db.Universities.FirstOrDefault(u => u.UniversityId == user.UniversityID);
                if (university == null)
                {
                    ModelState.AddModelError("", "University not found.");
                    return View(viewModel);
                }

                string filePath = null;

                if (viewModel.CertificationFile != null && viewModel.CertificationFile.ContentLength > 0)
                {
                    string uploadsFolder = Server.MapPath("~/UploadedCertificates");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    // Use the original filename as uploaded by the user
                    string originalFileName = Path.GetFileName(viewModel.CertificationFile.FileName);
                    string fullPath = Path.Combine(uploadsFolder, originalFileName);
                    viewModel.CertificationFile.SaveAs(fullPath);

                    // Save relative path in DB
                    filePath = "~/UploadedCertificates/" + originalFileName;
                }


                // Generate unique requisition number
                string requisitionNo = "REQ-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 5).ToUpper();
                // Save Master Table
                var purchaseOrder = new PurchaseOrder
                {
                    PODate = DateTime.Now,
                    //UniversityID = university.UniversityID,
                    UniversityName = university.UniversityName,
                    UniversityAddress = university.Address,
                    UniversityPhone = university.UniversityPhone,
                    UniversityEmail = university.UniversityEmail,
                    RequisitionNo = requisitionNo,
                    ShipTo = viewModel.ShipTo,
                    RequisitionedBy = university.UniversityName,
                    WhenShip = viewModel.WhenShip,
                    ShipVia = viewModel.ShipVia,
                    FOBPoint = viewModel.FOBPoint,
                    Terms = viewModel.Terms,
                    //CopiesOfInvoice = model.CopiesOfInvoice,
                    CopiesOfInvoice = viewModel.CopiesOfInvoice,
                    AuthorizedBy = viewModel.AuthorizedBy,
                    CreatedBy = userId.ToString(),
                    CreatedDateTime = DateTime.Now,
                    PurchaseDepartmentUploads = filePath,
                    Status = "Draft"
                };

                _db.PurchaseOrders.Add(purchaseOrder);
                _db.SaveChanges(); // This generates PONumber (Identity)

                // Save Item Rows
                foreach (var item in viewModel.PurchaseOrderItems)
                {
                    var poItem = new PurchaseOrderItem
                    {
                        PONumber = purchaseOrder.PONumber,
                        QtyOrdered = item.QtyOrdered,
                        QtyReceived = item.QtyReceived,
                        Description = item.Description,
                        UnitPrice = item.UnitPrice,
                        Total = item.Total,
                        VendorEmail = item.VendorEmail
                    };
                    _db.PurchaseOrderItems.Add(poItem);
                    // 🔁 Update TempSelectedMaterials status for this item
                    var tempMaterial = _db.TempSelectedMaterials
                                          .FirstOrDefault(t => t.UserID == userId &&
                                                               t.MaterialSubCategory == item.Description &&
                                                               t.Status == "New");

                    if (tempMaterial != null)
                    {
                        tempMaterial.Status = "Sent";
                        _db.Entry(tempMaterial).State = EntityState.Modified;
                    }
                }

                _db.SaveChanges();
                TempData["Success"] = "PO Created Successfully!";
                TempData["VendorEmail"] = viewModel.VendorEmail;

                return RedirectToAction("InitiatePOEmail", new { PONumber = purchaseOrder.PONumber });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                return View(viewModel); // fallback
            }
        }
         
        public ActionResult InitiatePOEmail(int PONumber)
        {
            string userId = (string)Session["UserID"];
            var user = _db.LocalPurchaseDepartments.FirstOrDefault(u => u.LocalID == userId);

            if (user == null)
                return HttpNotFound("Purchase department user not found.");

            string otp = new Random().Next(100000, 999999).ToString();
            Session["OTP"] = otp;
            Session["PONumberToSend"] = PONumber;
            Session["OTPAttempts"] = 0;

            //SendEmail(user.EmailID, "OTP for PO Email Confirmation", $"Your OTP is: {otp}");

            string toEmail = user.EmailID;
            string subject = $"Verification OTP for PO Email Confirmation – OTP:{otp}, PO No: {PONumber}";
            string body = $"Dear Purchase Department,\n\nPlease find attached the OTP  {otp}.\n\nRegards,\nICFAI ";

            Debug.WriteLine($"[DEBUG] Sending email to: {toEmail}");

            try
            {
                using (var message = new MailMessage())
                {
                    message.From = new MailAddress("shivaupputuri5@gmail.com");
                    message.To.Add(toEmail);
                    message.Subject = subject;
                    message.Body = body;
                    //message.Attachments.Add(new Attachment(new MemoryStream(pdfBytes), $"PO_{PONumber}.pdf"));

                    using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                    {
                        smtp.Credentials = new NetworkCredential("shivaupputuri5@gmail.com", "uxwvtphmvzhqqqpl");
                        smtp.EnableSsl = true;

                        smtp.Send(message);
                        Debug.WriteLine("[DEBUG] Email sent successfully.");
                    }
                }

                //TempData["OTP"] = true;
                TempData["PONumber"] = PONumber;

                return View("EnterOTP");
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to send PO.";
                return RedirectToAction("Materials"); // fallback
            }

           
        }


        [HttpPost]
        public ActionResult VerifyOTPAndSendPO(string enteredOTP)
        {
            string storedOTP = Session["OTP"]?.ToString();
            int attempts = (int)(Session["OTPAttempts"] ?? 0);

            if (storedOTP == enteredOTP)
            {
                int poNumber = (int)Session["PONumberToSend"];
                Session.Remove("OTP");
                Session.Remove("OTPAttempts");
                return RedirectToAction("SendMail", new { PONumber = poNumber });
            }

            attempts++;
            Session["OTPAttempts"] = attempts;

            if (attempts >= 3)
            {
                TempData["Error"] = "Failed to send PO: Too many incorrect OTP attempts.";
                return RedirectToAction("Materials");
            }

            ViewBag.Error = $"Invalid OTP. Attempt {attempts}/3.";
            return PartialView("_EnterOTPPartial");
        }



        public ActionResult SendMail(int PONumber)
        {
            Debug.WriteLine($"[DEBUG] Starting SendMail for PO: {PONumber}");

            var po = _db.PurchaseOrders.FirstOrDefault(p => p.PONumber == PONumber);
            if (po == null)
            {
                Debug.WriteLine("[ERROR] Purchase Order not found.");
                return HttpNotFound("PO not found");
            }

            Debug.WriteLine("[DEBUG] Purchase Order found.");

            var items = _db.PurchaseOrderItems.Where(i => i.PONumber == PONumber).ToList();
            Debug.WriteLine($"[DEBUG] Retrieved {items.Count} purchase order items.");

            // Prepare ViewModel for PDF
            var model = new GeneratePOViewModel
            {
                PONumber = po.PONumber.ToString(),
                UniversityName = po.UniversityName,
                UniversityAddress = po.UniversityAddress,
                UniversityPhone = po.UniversityPhone,
                UniversityEmail = po.UniversityEmail,
                RequisitionNo = po.RequisitionNo,
                ShipTo = po.ShipTo,
                RequisitionedBy = po.RequisitionedBy,
                WhenShip = po.WhenShip,
                ShipVia = po.ShipVia,
                FOBPoint = po.FOBPoint,
                Terms = po.Terms,
                CopiesOfInvoice = (int)po.CopiesOfInvoice,
                AuthorizedBy = po.AuthorizedBy,
                //PurchaseDepartmentUploads = po.PurchaseDepartmentUploads,
                PurchaseOrderItems = items.Select(item => new PurchaseOrderItem
                {
                    QtyOrdered = item.QtyOrdered ?? 0,
                    QtyReceived = 0,
                    Description = item.Description,
                    UnitPrice = item.UnitPrice ?? 0,
                    Total = (item.QtyOrdered ?? 0) * (item.UnitPrice ?? 0),
                    VendorEmail = item.VendorEmail
                }).ToList()
            };
            Debug.WriteLine("[DEBUG] ViewModel for PDF created.");
            Debug.WriteLine($"[DEBUG] CopiesOfInvoice in ViewModel = {model.CopiesOfInvoice}");

            Debug.WriteLine("[DEBUG] ViewModel for PDF created.");

            // Generate PDF from View`````````````````````````````
            var pdf = new Rotativa.ViewAsPdf("POPDFView", model)
            {
                FileName = $"PurchaseOrder_{PONumber}.pdf"
            };

            Debug.WriteLine("[DEBUG] Generating PDF...");
            var pdfBytes = pdf.BuildFile(ControllerContext);
            Debug.WriteLine($"[DEBUG] PDF generated. Size: {pdfBytes.Length} bytes");

            // Email Details
            string toEmail = TempData["VendorEmail"]?.ToString() ?? "default@vendor.com";
            string subject = $"Purchase Order – PO No: {PONumber}";
            string body = $"Dear Vendor,\n\nPlease find attached the Purchase Order #{PONumber}.\n\nRegards,\nICFAI Procurement Team";

            Debug.WriteLine($"[DEBUG] Sending email to: {toEmail}");

            try
            {
                using (var message = new MailMessage())
                {
                    message.From = new MailAddress("shivaupputuri5@gmail.com");
                    message.To.Add(toEmail);
                    message.Subject = subject;
                    message.Body = body;
                    message.Attachments.Add(new Attachment(new MemoryStream(pdfBytes), $"PO_{PONumber}.pdf"));

                    using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                    {
                        smtp.Credentials = new NetworkCredential("shivaupputuri5@gmail.com", "uxwvtphmvzhqqqpl");
                        smtp.EnableSsl = true;

                        smtp.Send(message);
                        Debug.WriteLine("[DEBUG] Email sent successfully.");
                    }
                }

                TempData["ShowSuccessPopup"] = true;
                TempData["PONumber"] = PONumber;

                return View("SendMailConfirmation");
            }
            catch (Exception )
            {
                TempData["Error"] = "Failed to send PO.";
                return RedirectToAction("Materials"); // fallback
            }
        }

        public ActionResult MyRequests()
        {
            string userId = (string)Session["UserID"];

            // Get all POs created by this user
            var purchaseOrders = _db.PurchaseOrders
                                    .Where(po => po.CreatedBy == userId.ToString())
                                    .OrderByDescending(po => po.PODate)
                                    .ToList();

            // Create a ViewModel list for each PO with its items
            var viewModel = purchaseOrders.Select(po => new PurchaseOrderGroupedViewModel
            {
                PONumber = po.PONumber,
                PODate = (DateTime)po.PODate,
                PurchaseOrderItems = _db.PurchaseOrderItems
                                        .Where(item => item.PONumber == po.PONumber)
                                        .ToList()
            }).ToList();

            return View(viewModel);
        }
    }

}

