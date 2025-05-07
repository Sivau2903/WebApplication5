using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Web;
using System.Web.Mvc;
using WebApplication5.Models;
using System.Data.Entity;
using Newtonsoft.Json;
using static System.Net.WebRequestMethods;

namespace WebApplication5.Controllers
{
    public class CentralPurchaseDepartmentController : BaseController
    {
        private readonly ASPEntities2 _db = new ASPEntities2();
        // GET: CentralPurchaseDepartment
        public ActionResult CPDDashBoard()
        {
            return View();
        }
        public ActionResult RequestsRecevied()
        {
            string purchaseDepartmentID = (string)Session["UserID"];

            var raisedRequests = (from request in _db.SavetoCentrals
                                  where request.CentralID == purchaseDepartmentID &&
                                        (request.Status == "Sent to Central" || request.Status == "Some in Processing")
                                  orderby request.Material, request.RequestedDate descending
                                  select new CentralRequestViewModel
                                  {
                                      ID = request.ID,
                                      UniversityID = request.UniversityID,
                                      UniversityName = _db.Universities
                                                           .Where(u => u.UniversityId == request.UniversityID)
                                                           .Select(u => u.UniversityName)
                                                           .FirstOrDefault(),
                                      MaterialName = request.Material,
                                      RequestedDate = request.RequestedDate,
                                      OrderQuantity = request.Order_Quantity,
                                      PurchaseDepartmentUploads = request.PurchaseDepartmentUploads,
                                      CentralID = request.CentralID,
                                      Status = request.Status
                                  }).ToList();

            return View(raisedRequests);
        }



        public JsonResult GetAvailableBudget()
        {
            string sessionID = (string)Session["UserID"]; // Adjust based on your session handling
            var budget = _db.CentralPurchaseDepartments
                            .Where(lpd => lpd.CentralID == sessionID)
                            .Select(lpd => lpd.Budget)
                            .FirstOrDefault();

            return Json(budget, JsonRequestBehavior.AllowGet);
        }

      

        public ActionResult GetVendors(string material)
        {
            Session["Material"] = material;
            var vendorDetails = _db.CentralVendorDetails
                .Where(v => v.Material == material)
                .ToList();

            return PartialView("_CentralVendorDetailsPartial", vendorDetails);
        }




        public ActionResult CentralGeneratePO(string material, int qty, string vendorId, decimal price, decimal cost, string email)
        {
            Debug.WriteLine("==== CentralGeneratePO (GET) called ====");
            Debug.WriteLine($"Incoming Params -> Material: {material}, Qty: {qty}, VendorId: {vendorId}, Price: {price}, Cost: {cost}, Email: {email}");

            // Fetch user info from session
            string userId = (string)Session["UserID"];
            Debug.WriteLine($"Session UserID: {userId}");

            var user = _db.CentralPurchaseDepartments.FirstOrDefault(u => u.CentralID == userId);
            if (user == null)
            {
                Debug.WriteLine("User not found in CentralPurchaseDepartments table");
                return HttpNotFound("User not found");
            }

            Debug.WriteLine($"User found: {user.CentralDepartmentName}, Email: {user.CentralDepartmentEmail}");

            // Generate unique requisition number
            string requisitionNo = "REQ-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 5).ToUpper();


            var viewModel = new CentralGeneratePOViewModel
            {
                VendorEmail = email,
                CentralDepartmentName = user.CentralDepartmentName,
                CentralDepartmentPhone = user.CentralDepartmentPhone,
                CentralDepartmentEmail = user.CentralDepartmentEmail,
                CentralDepartmentAddress = user.CentralDepartmentAddress,
                RequisitionedBy = user.CentralDepartmentName,
                RequisitionNo = requisitionNo,

                CentralPurchaseOrderItems = new List<CentralPurchaseOrderItem>
        {
            new CentralPurchaseOrderItem
            {
                Description = material,
                QtyOrdered = qty,
                UnitPrice = price,
                Total = cost,
                VendorEmail = email
            }
        }
            };

            Debug.WriteLine("PO ViewModel constructed successfully.");
            Debug.WriteLine($"Item Count: {viewModel.CentralPurchaseOrderItems.Count}");

            ViewBag.University = user;

            return View(viewModel);
        }





        [HttpPost]
        public ActionResult CentralGeneratePO(CentralGeneratePOViewModel viewModel)
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
                var user = _db.CentralPurchaseDepartments.FirstOrDefault(u => u.CentralID == userId);
                if (user == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    return View(viewModel);
                }

                // Generate unique requisition number
                string requisitionNo = "REQ-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 5).ToUpper();

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

                // Save Master Table
                var purchaseOrder = new CentralPurchaseOrder
                {
                    PODate = DateTime.Now,
                    //UniversityID = university.UniversityID,
                    CentralDepartmentName = user.CentralDepartmentName,
                    CentralDepartmentAddress = user.CentralDepartmentAddress,
                    CentralDepartmentPhone = user.CentralDepartmentPhone,
                    CentralDepartmentEmail = user.CentralDepartmentEmail,
                    RequisitionNo = requisitionNo,
                    ShipTo = viewModel.ShipTo,
                    RequisitionedBy = user.CentralDepartmentName,
                    WhenShip = viewModel.WhenShip,
                    ShipVia = viewModel.ShipVia,
                    FOBPoint = viewModel.FOBPoint,
                    Terms = viewModel.Terms,
                    
                    CopiesOfInvoice = viewModel.CopiesOfInvoice,
                    AuthorizedBy = viewModel.AuthorizedBy,
                    CreatedBy = userId.ToString(),
                    PurchaseDepartmentUploads = filePath,


                    Status = "Draft"
                };

                _db.CentralPurchaseOrders.Add(purchaseOrder);
                _db.SaveChanges(); // This generates PONumber (Identity)

                // Save Item Rows
                if (viewModel.CentralPurchaseOrderItems != null)
                {
                    foreach (var item in viewModel.CentralPurchaseOrderItems)
                    {
                        var poItem = new CentralPurchaseOrderItem
                        {
                            PONumber = purchaseOrder.PONumber,
                            QtyOrdered = item.QtyOrdered,
                            QtyReceived = item.QtyReceived,
                            Description = item.Description,
                            UnitPrice = item.UnitPrice,
                            Total = item.Total,
                            VendorEmail = item.VendorEmail
                        };
                        _db.CentralPurchaseOrderItems.Add(poItem);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "No purchase order items were submitted.");
                    return View(viewModel);
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


        public ActionResult SendMail(int PONumber)
        {
            Debug.WriteLine($"[DEBUG] Starting SendMail for PO: {PONumber}");

            var po = _db.CentralPurchaseOrders.FirstOrDefault(p => p.PONumber == PONumber);
            if (po == null)
            {
                Debug.WriteLine("[ERROR] Purchase Order not found.");
                return HttpNotFound("PO not found");
            }

            Debug.WriteLine("[DEBUG] Purchase Order found.");

            var items = _db.CentralPurchaseOrderItems.Where(i => i.PONumber == PONumber).ToList();
            Debug.WriteLine($"[DEBUG] Retrieved {items.Count} purchase order items.");



            // Prepare ViewModel for PDF
            var model = new CentralGeneratePOViewModel
            {
                PONumber = po.PONumber.ToString(),
                CentralDepartmentName = po.CentralDepartmentName,
                CentralDepartmentAddress = po.CentralDepartmentAddress,
                CentralDepartmentPhone = po.CentralDepartmentPhone,
                CentralDepartmentEmail = po.CentralDepartmentEmail,
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
                CentralPurchaseOrderItems = items.Select(item => new CentralPurchaseOrderItem
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

            // Generate PDF from View
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

                // Update status of relevant materials in SavetoCentral table
                // Update status of all relevant materials in SavetoCentral table
                foreach (var item in items)
                {
                    var matchingRequests = _db.SavetoCentrals
                        .Where(r => r.Material == item.Description && r.Status != "Sent for Purchase")
                        .ToList();

                    foreach (var request in matchingRequests)
                    {
                        request.Status = "Sent for Purchase";
                    }
                }

                _db.SaveChanges();
                Debug.WriteLine("[DEBUG] Updated all matching rows to 'Sent for Purchase' in SavetoCentral table.");



                TempData["ShowSuccessPopup"] = true;
                TempData["PONumber"] = PONumber;

                return View("SendMailConfirmation");
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to send PO.";
                return RedirectToAction("RequestsRecieved"); // fallback
            }
        }

        public ActionResult MyRequests()
        {
            string userId = (string)Session["UserID"];

            // Get all POs created by this user
            var purchaseOrders = _db.CentralPurchaseOrders
                                    .Where(po => po.CreatedBy == userId.ToString())
                                    .OrderByDescending(po => po.PODate)
                                    .ToList();

            // Create a ViewModel list for each PO with its items
            var viewModel = purchaseOrders.Select(po => new CentralPurchaseOrderGroupedViewModel
            {
                PONumber = po.PONumber,
                PODate = (DateTime)po.PODate,
                CentralPurchaseOrderItems = _db.CentralPurchaseOrderItems
                                        .Where(item => item.PONumber == po.PONumber)
                                        .ToList()
            }).ToList();

            return View(viewModel);
        }

        [HttpPost]
        public ActionResult GenerateBulkPO(string vendorId, string email, string materials)
        {
            if (string.IsNullOrEmpty(materials))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Materials data is missing.");
            }

            var materialList = JsonConvert.DeserializeObject<List<MaterialItem>>(materials);

            string userId = (string)Session["UserID"];
            var user = _db.CentralPurchaseDepartments.FirstOrDefault(u => u.CentralID == userId);
            if (user == null) return HttpNotFound("User not found");

            var university = _db.CentralPurchaseDepartments.FirstOrDefault(u => u.CentralID == user.CentralID);
            if (university == null) return HttpNotFound("University not found");

            string requisitionNo = "REQ-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 5).ToUpper();

            var viewModel = new CentralGeneratePOViewModel
            {
                VendorEmail = email,
                CentralDepartmentName = university.CentralDepartmentName,
                CentralDepartmentPhone = university.CentralDepartmentPhone,
                CentralDepartmentEmail = university.CentralDepartmentEmail,
                CentralDepartmentAddress = university.CentralDepartmentAddress,
                RequisitionedBy = university.CentralDepartmentName,
                AuthorizedBy = university.CentralDepartmentName,
                RequisitionNo = requisitionNo,
                CentralPurchaseOrderItems = materialList.Select(m => new CentralPurchaseOrderItem
                {
                    Description = m.MaterialName,
                    QtyOrdered = m.QtyOrdered,
                    UnitPrice = m.UnitPrice,
                    Total = m.Total,
                    //VendorID = Convert.ToInt32(vendorId),
                    VendorEmail = email
                }).ToList()
            };

            ViewBag.University = university;
            return View("GenerateBulkPO", viewModel);
        }


        //[HttpPost]
        //public ActionResult GenerateBulkPO( BulkPORequest request)
        //{
        //    if (request == null || request.Materials == null || !request.Materials.Any())
        //    {
        //        return new HttpStatusCodeResult(400, "Materials data is missing");
        //    }

        //    int userId = Convert.ToInt32(Session["UserID"]);
        //    var user = _db.CentralPurchaseDepartments.FirstOrDefault(u => u.CentralID == userId);
        //    if (user == null) return HttpNotFound("User not found");

        //    string requisitionNo = "REQ-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 5).ToUpper();

        //    var viewModel = new CentralGeneratePOViewModel
        //    {
        //        VendorEmail = request.Email,
        //        CentralDepartmentName = user.CentralDepartmentName,
        //        CentralDepartmentPhone = user.CentralDepartmentPhone,
        //        CentralDepartmentEmail = user.CentralDepartmentEmail,
        //        CentralDepartmentAddress = user.CentralDepartmentAddress,
        //        RequisitionedBy = user.CentralDepartmentName,
        //        AuthorizedBy = user.CentralDepartmentName,
        //        RequisitionNo = requisitionNo,
        //        CentralPurchaseOrderItems = request.Materials.Select(m => new CentralPurchaseOrderItem
        //        {
        //            Description = m.MaterialName,
        //            QtyOrdered = m.QtyOrdered,
        //            UnitPrice = m.UnitPrice,
        //            Total = m.Total,
        //            VendorEmail = request.Email
        //        }).ToList()
        //    };

        //    ViewBag.University = user;
        //    return View("GenerateBulkPO", viewModel);
        //}




        [HttpPost]
        public ActionResult GenerateBulkPO123(CentralGeneratePOViewModel viewModel)
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
                var user = _db.CentralPurchaseDepartments.FirstOrDefault(u => u.CentralID == userId);
                if (user == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    return View(viewModel);
                }

                var university = _db.CentralPurchaseDepartments.FirstOrDefault(u => u.CentralID == user.CentralID);
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
                var purchaseOrder = new CentralPurchaseOrder
                {
                    PODate = DateTime.Now,
                    //UniversityID = university.UniversityID,
                    CentralDepartmentName = university.CentralDepartmentName,
                    CentralDepartmentAddress = university.CentralDepartmentAddress,
                    CentralDepartmentPhone = university.CentralDepartmentPhone,
                    CentralDepartmentEmail = university.CentralDepartmentEmail,
                    RequisitionNo = requisitionNo,
                    ShipTo = viewModel.ShipTo,
                    RequisitionedBy = university.CentralDepartmentName,
                    WhenShip = viewModel.WhenShip,
                    ShipVia = viewModel.ShipVia,
                    FOBPoint = viewModel.FOBPoint,
                    Terms = viewModel.Terms,
                    //CopiesOfInvoice = model.CopiesOfInvoice,
                    CopiesOfInvoice = viewModel.CopiesOfInvoice,
                    AuthorizedBy = viewModel.AuthorizedBy,
                    CreatedBy = userId.ToString(),
                    //CreatedDateTime = DateTime.Now,
                    //Remarks = null,
                    PurchaseDepartmentUploads = filePath,
                    Status = "Draft"
                };

                _db.CentralPurchaseOrders.Add(purchaseOrder);
                _db.SaveChanges(); // This generates PONumber (Identity)

                // Save Item Rows
                foreach (var item in viewModel.CentralPurchaseOrderItems)
                {
                    var poItem = new CentralPurchaseOrderItem
                    {
                        PONumber = purchaseOrder.PONumber,
                        QtyOrdered = item.QtyOrdered,
                        QtyReceived = item.QtyReceived,
                        Description = item.Description,
                        UnitPrice = item.UnitPrice,
                        Total = item.Total,
                        VendorEmail = item.VendorEmail
                    };
                    _db.CentralPurchaseOrderItems.Add(poItem);
                    // 🔁 Update TempSelectedMaterials status for this item
                    //    var tempMaterial = _db.TempSelectedMaterials
                    //                          .FirstOrDefault(t => t.UserID == userId &&
                    //                                               t.MaterialSubCategory == item.Description &&
                    //                                               t.Status == "New");

                    //    if (tempMaterial != null)
                    //    {
                    //        tempMaterial.Status = "Sent";
                    //        _db.Entry(tempMaterial).State = EntityState.Modified;
                    //    }
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
            var user = _db.CentralPurchaseDepartments.FirstOrDefault(u => u.CentralID == userId.ToString());

            if (user == null)
                return HttpNotFound("Purchase department user not found.");

            string otp = new Random().Next(100000, 999999).ToString();
            Session["OTP"] = otp;
            Session["PONumberToSend"] = PONumber;
            Session["OTPAttempts"] = 0;

            //SendEmail(user.EmailID, "OTP for PO Email Confirmation", $"Your OTP is: {otp}");

            string toEmail = user.CentralDepartmentEmail;
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
                return RedirectToAction("RequestsReceived"); // fallback
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
                return RedirectToAction("RequestsReceived");
            }

            ViewBag.Error = $"Invalid OTP. Attempt {attempts}/3.";
            return View("EnterOTP");
        }

    }
}