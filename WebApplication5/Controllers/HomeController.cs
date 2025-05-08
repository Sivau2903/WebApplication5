using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.UI;
using WebApplication5.Models;

namespace WebApplication5.Controllers
{
    
    public class HomeController : BaseController
    {
        private readonly ASPEntities2 _db = new ASPEntities2();
        private List<HODRequestGroupedViewModel> pagedrequests;

        public ActionResult StoreAdminDasBoard(DateTime? fromDate, DateTime? toDate, string requestType = "Employee", int page = 1, int pageSize = 10)
        {
          
            return View();
        }


        public ActionResult IssueForm(int requestId, string msubCategory)
        {
            Console.WriteLine($"Received IssueForm Call: {requestId}, {msubCategory}");

            // ✅ 1. Ensure the request is an AJAX request
            if (!Request.IsAjaxRequest())
            {
                return Json(new { success = false, message = "Request is not AJAX." });
            }

            // ✅ 2. Identify the currently logged-in StoreAdmin
            string userID = Session["UserID"] as string;
            if (string.IsNullOrEmpty(userID))
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] User session is empty. Cannot find StoreAdmin.");
                return Json(new { success = false, message = "User session expired. Please log in again." });
            }

            var storeAdmin = _db.StoreAdmins.FirstOrDefault(s => s.StoreAdminID.ToString() == userID);
            if (storeAdmin == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] StoreAdmin not found in database.");
                return Json(new { success = false, message = "StoreAdmin not found." });
            }

            System.Diagnostics.Debug.WriteLine($"[DEBUG] Found StoreAdmin => ID={storeAdmin.StoreAdminID}");

            // ✅ 3. Retrieve the request from the database
            var request = _db.Requests.FirstOrDefault(r => r.RequestID == requestId && r.MSubCategory == msubCategory);
            if (request == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Request not found in DB!");
                return Json(new { success = false, message = "Request not found." });
            }

            // ✅ 4. Fetch Available Quantity directly from the Request table
            int issuingQuantity = request.IssuingQuantity ?? 0;
            int availableQuantity = request.AvailableQuantity ?? 0;

            System.Diagnostics.Debug.WriteLine($"[DEBUG] Found Request => ID={request.RequestID}, AssetType={request.AssetType}, ApprovedQuantity={request.ApprovedQuantity}, AvailableQuantity={availableQuantity}");

            // ✅ 5. Create the EmployeeIssueMaterial model
            var issuingModel = new EmployeeIssueMaterial
            {
                RequestID = request.RequestID??0,
                EmpID = request.EmpID,
                HODID = request.HODID,
                AssetType = request.AssetType,
                MaterialCategory = request.MaterialCategory,
                MaterialSubCategory = request.MSubCategory,
                RequestingQuantity = request.RequestingQuantity,
                ApprovedQuantity = request.ApprovedQuantity ?? 0, // Ensure non-null
                AvailableQuantity = availableQuantity,  // ✅ Fetching directly from Request table
                IssuingQuantity = issuingQuantity, // Pre-fill existing IssuingQuantity
                PreviousIssuingQuantity = issuingQuantity, // Add this property to help in validation
                ClosingQuantity = 0,
                Issue = 0,
                IssuedBy = storeAdmin.StoreAdminID.ToString() // ✅ Correct assignment
            };

            System.Diagnostics.Debug.WriteLine($"[DEBUG] issuingModel => RequestID={issuingModel.RequestID}, ApprovedQty={issuingModel.ApprovedQuantity}, AvailQty={issuingModel.AvailableQuantity}");

            // ✅ 6. Return partial view with model
            return PartialView("_IssueForm", issuingModel);
        }


        [HttpPost]
        public ActionResult IssueMaterial(EmployeeIssueMaterial model)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] IssueMaterial => RequestID={model.RequestID}, IssuingQty={model.IssuingQuantity}");

            try
            {
                var request = _db.Requests.FirstOrDefault(m => m.RequestID == model.RequestID);
                if (request == null)
                {
                    TempData["ErrorMessage"] = "Request not found.";
                    return RedirectToAction("EmployeeRequests", "Home");
                }

                int approvedQty = request.ApprovedQuantity ?? 0;
                int previousIssuedQty = request.IssuingQuantity ?? 0;

                // Add the new issue to previousIssuedQty
                int newIssueInput = model.Issue ?? 0;
                int totalIssuingQty = previousIssuedQty + newIssueInput;

                if (totalIssuingQty > approvedQty)
                {
                    TempData["ErrorMessage"] = $"Total issuing quantity ({totalIssuingQty}) exceeds approved quantity ({approvedQty}).";
                    return RedirectToAction("EmployeeRequests", "Home");
                }

                if (newIssueInput > request.AvailableQuantity)
                {
                    TempData["ErrorMessage"] = $"New issuing quantity ({newIssueInput}) exceeds available stock ({request.AvailableQuantity}).";
                    return RedirectToAction("EmployeeRequests", "Home");
                }

                int closingQty = (request.AvailableQuantity ?? 0) - newIssueInput;

                // 🔍 UPDATE existing record in EmployeeIssueMaterial table
                var existingIssue = _db.EmployeeIssueMaterials.FirstOrDefault(e => e.RequestID == model.RequestID);
                if (existingIssue != null)
                {
                    existingIssue.IssuingQuantity = totalIssuingQty;
                    existingIssue.ClosingQuantity = closingQty;
                    existingIssue.IssuedDate = DateTime.Now;
                    existingIssue.Status = "Issued";
                    existingIssue.IssuedBy = model.IssuedBy;
                }
                else
                {
                    // If record not found, create new one (fallback)
                    model.IssuingQuantity = totalIssuingQty;
                    model.ClosingQuantity = closingQty;
                    model.IssuedDate = DateTime.Now;
                    model.Status = "Issued";
                    _db.EmployeeIssueMaterials.Add(model);
                }

                // Update MaterialMasterList
                var masterItem = _db.MaterialMasterLists.FirstOrDefault(m =>
                    m.AssetType == model.AssetType &&
                    m.MaterialCategory == model.MaterialCategory &&
                    m.MaterialSubCategory == model.MaterialSubCategory
                );

                if (masterItem != null)
                {
                    masterItem.AvailableQuantity = closingQty;
                    masterItem.MaterialUpdatedDate = DateTime.Now;
                    masterItem.UpdatedBy = model.IssuedBy;
                }

                // Update Request table
                request.IssuingQuantity = totalIssuingQty;
                request.AvailableQuantity = closingQty;
                request.Status = (request.IssuingQuantity == request.RequestingQuantity) ? "Issued" : "Ongoing";

                _db.SaveChanges();

                TempData["SuccessMessage"] = "Material issued successfully!";
                return RedirectToAction("EmployeeRequests", "Home");
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                foreach (var eve in ex.EntityValidationErrors)
                {
                    foreach (var ve in eve.ValidationErrors)
                    {
                        System.Diagnostics.Debug.WriteLine($"- Property: \"{ve.PropertyName}\", Error: \"{ve.ErrorMessage}\"");
                    }
                }

                TempData["ErrorMessage"] = "Validation failed.";
                throw;
            }
        }





        public ActionResult HODIssueForm(int hodrequestId, string msubCategory)
        {
            Console.WriteLine($"Received IssueForm Call: {hodrequestId}, {msubCategory}");

            // ✅ 1. Ensure the request is an AJAX request
            if (!Request.IsAjaxRequest())
            {
                return Json(new { success = false, message = "Request is not AJAX." });
            }

            // ✅ 2. Identify the currently logged-in StoreAdmin
            string userID = Session["UserID"] as string;
            if (string.IsNullOrEmpty(userID))
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] User session is empty. Cannot find StoreAdmin.");
                return Json(new { success = false, message = "User session expired. Please log in again." });
            }

            var storeAdmin = _db.StoreAdmins.FirstOrDefault(s => s.StoreAdminID.ToString() == userID);
            if (storeAdmin == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] StoreAdmin not found in database.");
                return Json(new { success = false, message = "StoreAdmin not found." });
            }

            System.Diagnostics.Debug.WriteLine($"[DEBUG] Found StoreAdmin => ID={storeAdmin.StoreAdminID}");

            // ✅ 3. Retrieve the request from the database
            var request = _db.HODRequests.FirstOrDefault(r => r.HODRequestID == hodrequestId && r.MSubCategory == msubCategory);
            if (request == null)
            {
                return Json(new { success = false, message = "Request not found." });
            }

            // Fetching IssuingQuantity from DB
            int issuingQuantity = request.IssuingQuantity ?? 0;
            int availableQuantity = request.AvailableQuantity ?? 0;

            var issuingModel = new HODIssueMaterial
            {
                RequestID = request.HODRequestID ?? 0,
                HODID = request.HODID,
                AssetType = request.AssetType,
                MaterialCategory = request.MaterialCategory,
                MaterialSubCategory = request.MSubCategory,
                RequestingQuantity = request.RequestingQuantity,
                AvailableQuantity = availableQuantity,
                IssuingQuantity = issuingQuantity, // Pre-fill existing IssuingQuantity
                PreviousIssuingQuantity = issuingQuantity, // Add this property to help in validation
                ClosingQuantity = 0,
                Issue = 0,
                IssuedBy = storeAdmin.StoreAdminID.ToString()
            };

            return PartialView("_HODIssueForm", issuingModel);

        }

        [HttpPost]
        public ActionResult HODIssueMaterial(HODIssueMaterial model)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] IssueMaterial => RequestID={model.RequestID}, IssuingQty={model.IssuingQuantity}");

            try
            {
                var request = _db.HODRequests.FirstOrDefault(m => m.HODRequestID == model.RequestID);
                if (request == null)
                {
                    TempData["ErrorMessage"] = "Request not found.";
                    return RedirectToAction("HODRequests", "Home");
                }

                int approvedQty = request.RequestingQuantity ;
                int previousIssuedQty = request.IssuingQuantity ?? 0;

                // Add the new issue to previousIssuedQty
                int newIssueInput = model.Issue ?? 0;
                int totalIssuingQty = previousIssuedQty + newIssueInput;

                if (totalIssuingQty > approvedQty)
                {
                    TempData["ErrorMessage"] = $"Total issuing quantity ({totalIssuingQty}) exceeds approved quantity ({approvedQty}).";
                    return RedirectToAction("HODRequests", "Home");
                }

                if (newIssueInput > request.AvailableQuantity)
                {
                    TempData["ErrorMessage"] = $"New issuing quantity ({newIssueInput}) exceeds available stock ({request.AvailableQuantity}).";
                    return RedirectToAction("HODRequests", "Home");
                }

                int closingQty = (request.AvailableQuantity ?? 0) - newIssueInput;

                // 🔍 UPDATE existing record in EmployeeIssueMaterial table
                var existingIssue = _db.HODIssueMaterials.FirstOrDefault(e => e.RequestID == model.RequestID);
                if (existingIssue != null)
                {
                    existingIssue.IssuingQuantity = totalIssuingQty;
                    existingIssue.ClosingQuantity = closingQty;
                    existingIssue.IssuedDate = DateTime.Now;
                    existingIssue.Status = "Issued";
                    existingIssue.IssuedBy = model.IssuedBy;
                }
                else
                {
                    // If record not found, create new one (fallback)
                    model.IssuingQuantity = totalIssuingQty;
                    model.ClosingQuantity = closingQty;
                    model.IssuedDate = DateTime.Now;
                    model.Status = "Issued";
                    _db.HODIssueMaterials.Add(model);
                }

                // Update MaterialMasterList
                var masterItem = _db.MaterialMasterLists.FirstOrDefault(m =>
                    m.AssetType == model.AssetType &&
                    m.MaterialCategory == model.MaterialCategory &&
                    m.MaterialSubCategory == model.MaterialSubCategory
                );

                if (masterItem != null)
                {
                    masterItem.AvailableQuantity = closingQty;
                    masterItem.MaterialUpdatedDate = DateTime.Now;
                    masterItem.UpdatedBy = model.IssuedBy;
                }

                // Update Request table
                request.IssuingQuantity = totalIssuingQty;
                request.AvailableQuantity = closingQty;
                request.Status = (request.IssuingQuantity == request.RequestingQuantity) ? "Issued" : "Ongoing";

                _db.SaveChanges();

                TempData["SuccessMessage"] = "Material issued successfully!";
                return RedirectToAction("HODRequests", "Home");
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                foreach (var eve in ex.EntityValidationErrors)
                {
                    foreach (var ve in eve.ValidationErrors)
                    {
                        System.Diagnostics.Debug.WriteLine($"- Property: \"{ve.PropertyName}\", Error: \"{ve.ErrorMessage}\"");
                    }
                }

                TempData["ErrorMessage"] = "Validation failed.";
                throw;
            }
        }



        public ActionResult EmployeeRequests(DateTime? fromDate, DateTime? toDate, string requestType = "Employee", int page = 1, int pageSize = 10)
        {

            // Validate session
            string userID = Session["UserID"] as string;
            string userRole = Session["UserRole"] as string;

            System.Diagnostics.Debug.WriteLine($"Session ID: {userID}, Role: {userRole}");

            if (string.IsNullOrEmpty(userID) || userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                // Return to some safe page (avoid infinite loop)
                return RedirectToAction("StoreAdminDasBoard", "Home");
            }

            // Find the StoreAdmin
            var storeadmin = _db.StoreAdmins.FirstOrDefault(e => e.StoreAdminID.ToString() == userID);
            if (storeadmin == null)
            {
                System.Diagnostics.Debug.WriteLine("Error: StoreAdmin not found in the database for the given email.");
                TempData["ErrorMessage"] = "StoreAdmin details not found.";
                return RedirectToAction("StoreAdminDasBoard", "Home");
            }

            string storeadminID = storeadmin.StoreAdminID;
            System.Diagnostics.Debug.WriteLine($"StoreAdminID Retrieved: {storeadminID}");



            // Base query: Approved or Ongoing requests for this StoreAdmin
            var query = _db.Requests
                .Where(r => r.StoreAdminID == storeadminID
                         && (r.Status == "Approved" || r.Status == "Ongoing"))
                .OrderByDescending(r => r.RequestDate)
                .ToList()
                .GroupBy(r => r.RequestID)
                .Select((group, index) => new RequestGroupedViewModel
                {
                    SNo = index + 1,
                    RequestID = group.Key ?? 0,
                    EmpID = group.First().EmpID,
                    RequestDate = group.First().RequestDate ?? DateTime.Now,
                    Status = group.First().Status ?? "Unknown",
                    AssetDetails = group.Select(r => new RequestViewModel
                    {
                        AssetType = r.AssetType ?? "N/A",
                        MaterialCategory = r.MaterialCategory ?? "N/A",
                        MSubCategory = r.MSubCategory ?? "N/A",
                        AvailableQuantity = r.AvailableQuantity ?? 0,
                        RequestingQuantity = r.RequestingQuantity,
                        ApprovedQuantity = r.ApprovedQuantity ?? 0,
                        //IssuingQuantity = r.IssuingQuantity ?? 0,
                        PendingQuantity = r.PendingQuantity ?? 0
                    }).ToList()
                })
                .ToList();

            // Apply filters
            if (fromDate.HasValue)
            {
                query = query.Where(r => r.RequestDate >= fromDate.Value).ToList();
            }
            if (toDate.HasValue)
            {
                query = query.Where(r => r.RequestDate <= toDate.Value).ToList();
            }

            // Pagination
            int totalRequests = query.Count();
            var pagedRequests = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Create the wrapper ViewModel
            var model = new HODDashboardViewModel
            {
                Requests = pagedRequests,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalRequests
            };

            return View(model);

        } 
        
        public ActionResult IssuedRequest()
        {

            var userID = Session["UserID"] as string;
            var userRole = Session["UserRole"] as string;

            if (string.IsNullOrEmpty(userID) || userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                return RedirectToAction("StoreAdminDasBoard");
            }

            var storeAdmin = _db.StoreAdmins.FirstOrDefault(e => e.StoreAdminID.ToString() == userID);
            if (storeAdmin == null)
            {
                TempData["ErrorMessage"] = "StoreAdmin details not found.";
                return RedirectToAction("StoreAdminDasBoard");
            }

            // Step 1: Fetch issued materials
            var issuedRequests = _db.EmployeeIssueMaterials
                .Where(r => r.IssuedBy == storeAdmin.StoreAdminID.ToString() && r.Status == "Issued")
                .OrderByDescending(r => r.IssuedDate)
                .ToList();

            // Step 2: Group by RequestID
            var groupedData = issuedRequests
                .GroupBy(r => r.RequestID)
                .Select(g => new IssueGroupedViewModel
                {
                    RequestID = g.Key,
                    RequestDate = g.First().IssuedDate,
                    Materials = g.ToList() // All materials under this RequestID
                })
                .ToList();

            return View(groupedData);
        }




        public ActionResult HODRequests(DateTime? fromDate, DateTime? toDate, string requestType = "Employee", int page = 1, int pageSize = 10)
        {
        // Validate session
            string userID = Session["UserID"] as string;
        string userRole = Session["UserRole"] as string;

        System.Diagnostics.Debug.WriteLine($"Session ID: {userID}, Role: {userRole}");

            if (string.IsNullOrEmpty(userID) || userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                // Return to some safe page (avoid infinite loop)
                return RedirectToAction("StoreAdminDasBoard", "Home");
            }

            // Find the StoreAdmin
            var storeadmin = _db.StoreAdmins.FirstOrDefault(e => e.StoreAdminID.ToString() == userID);
            if (storeadmin == null)
            {
                System.Diagnostics.Debug.WriteLine("Error: StoreAdmin not found in the database for the given email.");
                TempData["ErrorMessage"] = "StoreAdmin details not found.";
                return RedirectToAction("StoreAdminDasBoard", "Home");
}

string storeadminID = storeadmin.StoreAdminID;
System.Diagnostics.Debug.WriteLine($"StoreAdminID Retrieved: {storeadminID}");



// Base query: Approved or Ongoing requests for this StoreAdmin
var query = _db.HODRequests
    .Where(r => r.StoreAdminID == storeadminID
             && (r.Status == "New" || r.Status == "Ongoing"))
    .OrderByDescending(r => r.RequestedDate)
    .ToList()
    .GroupBy(r => r.HODRequestID)
    .Select((group, index) => new HODRequestGroupedViewModel
    {
        SNo = index + 1,
        HODRequestID = group.Key ?? 0,
        HODID = group.First().HODID,
        RequestedDate = group.First().RequestedDate ?? DateTime.Now,
        Status = group.First().Status ?? "Unknown",
        AssetDetails = group.Select(r => new HODRequestViewModel
        {
            AssetType = r.AssetType ?? "N/A",
            MaterialCategory = r.MaterialCategory ?? "N/A",
            MSubCategory = r.MSubCategory ?? "N/A",
            AvailableQuantity = r.AvailableQuantity ?? 0,
            RequestingQuantity = r.RequestingQuantity,
            //ApprovedQuantity = r.ApprovedQuantity ?? 0,
            //IssuingQuantity = r.IssuingQuantity ?? 0,
            //PendingQuantity = r.PendingQuantity ?? 0
        }).ToList()
    })
    .ToList();

// Apply filters
if (fromDate.HasValue)
{
    query = query.Where(r => r.RequestedDate >= fromDate.Value).ToList();
}
if (toDate.HasValue)
{
    query = query.Where(r => r.RequestedDate <= toDate.Value).ToList();
}

// Pagination
int totalRequests = query.Count();
var pagedRequests = query
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToList();

// Create the wrapper ViewModel
var model = new HODDashboardViewModel
{
    HODRequests = pagedRequests,
    CurrentPage = page,
    PageSize = pageSize,
    TotalCount = totalRequests
};

return View(model);
        
        }

        [HttpPost]
        public JsonResult RejectHODRequest(int hodrequestId)
        {
            try
            {
                var requests = _db.HODRequests.Where(r => r.HODRequestID == hodrequestId).ToList();

                if (requests.Count > 0)
                {
                    foreach (var req in requests)
                    {
                        req.Status = "Rejected";
                    }

                    _db.SaveChanges();

                    return Json(new { success = true });
                }
                else
                {
                    return Json(new { success = false, message = "Request not found." });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rejecting request: {ex.Message}");
                return Json(new { success = false, message = "Error occurred." });
            }
        }


        public ActionResult IssuedRequests()
        {


            var userID = Session["UserID"] as string;
            var userRole = Session["UserRole"] as string;

            if (string.IsNullOrEmpty(userID) || userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                return RedirectToAction("StoreAdminDasBoard");
            }

            var storeAdmin = _db.StoreAdmins.FirstOrDefault(e => e.StoreAdminID.ToString() == userID);
            if (storeAdmin == null)
            {
                TempData["ErrorMessage"] = "StoreAdmin details not found.";
                return RedirectToAction("StoreAdminDasBoard");
            }

            // Step 1: Fetch issued materials
            var issuedRequests = _db.HODIssueMaterials
                .Where(r => r.IssuedBy == storeAdmin.StoreAdminID.ToString() && r.Status == "Issued")
                .OrderByDescending(r => r.IssuedDate)
                .ToList();

            // Step 2: Group by RequestID
            var groupedData = issuedRequests
                .GroupBy(r => r.RequestID)
                .Select(g => new HODIssueGroupedViewModel
                {
                    RequestID = g.Key,
                    RequestDate = g.First().IssuedDate ?? DateTime.Now,

                    Materials = g.ToList() // All materials under this RequestID
                })
                .ToList();

            return View(groupedData);
        }



        public ActionResult MaterialMasterList()
        {
            // Fetch Asset Types from the database
            var assetTypes = _db.AssetTypes.Select(a => new
            {
                a.AssetTypeID,
                a.AssetType1
            }).ToList();

            ViewBag.AssetType = new SelectList(assetTypes, "AssetTypeID", "AssetType1");

            // Fetch materials to display on the page
            return View(_db.MaterialMasterLists.ToList());
            // Return an empty model to reset form

        }
        [HttpGet]
        public JsonResult GetMaterialCategories(string assetTypeName)
        {
            var categories = _db.MaterialCategories
                               .Where(m => m.AssetType.AssetType1 == assetTypeName) // Assuming AssetType navigation property
                               .Select(m => new
                               {
                                   m.MaterialCategory1
                               })
                               .ToList();

            return Json(categories, JsonRequestBehavior.AllowGet);
        }


       [HttpGet]
public JsonResult GetMaterialSubCategories(string categoryName)
{
    var subcategories = _db.MaterialSubCategories
                           .Where(m => m.MaterialCategory.MaterialCategory1 == categoryName) // Assuming navigation
                           .Select(m => new
                           {
                               m.MaterialSubCategory1
                           })
                           .ToList();

    return Json(subcategories, JsonRequestBehavior.AllowGet);
}





        [HttpGet]
        public JsonResult CheckMaterialMaster(string assetTypes, string categories, string subcategories)
        {
            if (string.IsNullOrEmpty(assetTypes) || string.IsNullOrEmpty(categories) || string.IsNullOrEmpty(subcategories))
            {
                // If any field missing, return exists = false
                return Json(new { exists = false }, JsonRequestBehavior.AllowGet);
            }

            // Directly checking by Names (NO IDs used here!)
            var item = _db.MaterialMasterLists
                .FirstOrDefault(m =>
                    m.AssetType.Equals(assetTypes, StringComparison.OrdinalIgnoreCase) &&
                    m.MaterialCategory.Equals(categories, StringComparison.OrdinalIgnoreCase) &&
                    m.MaterialSubCategory.Equals(subcategories, StringComparison.OrdinalIgnoreCase)
                );

            if (item != null)
            {
                // Entry found, return existing details
                return Json(new
                {
                    exists = true,
                    availableQuantity = item.AvailableQuantity,
                    make = item.Make,
                    unit = item.Units,
                    minimumLimit = item.MinimumLimit,
                    expiryDate = item.ExpiryDate?.ToString("yyyy-MM-dd")
                }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                // Entry doesn't exist
                return Json(new { exists = false }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpPost]
        public ActionResult MaterialMasterForm(MaterialMasterList model)
        {
            // Check if combination exists
            var existing = _db.MaterialMasterLists
                .FirstOrDefault(m =>
                    m.AssetType == model.AssetType &&
                    m.MaterialCategory == model.MaterialCategory &&
                    m.MaterialSubCategory == model.MaterialSubCategory
                );

            if (existing != null)
            {
                // Update existing row
                existing.AvailableQuantity = model.AvailableQuantity;
                existing.Make = model.Make;
                existing.Units = model.Units;
                existing.ExpiryDate = model.ExpiryDate;
                existing.MinimumLimit = model.MinimumLimit;
                existing.MaterialUpdatedDate = DateTime.Now;
                existing.UpdatedBy = "StoreAdmin"; // or from session

                _db.SaveChanges();
            }
            else
            {
                // Create new row
                model.MaterialUpdatedDate = DateTime.Now;
                model.UpdatedBy = "StoreAdmin"; // or from session

                _db.MaterialMasterLists.Add(model);

                _db.SaveChanges();
            }

            TempData["SuccessMessage"] = "Material saved successfully!";
            return RedirectToAction("MaterialMasterList", "Home");
        }

        


        public ActionResult AddAssetType()
        {

            return View();
        }

        // POST: AssetType/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssetTypeView(AssetType model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    AssetType assetType = new AssetType
                    {
                        AssetType1 = model.AssetType1?.Trim() // Ensure no null or whitespace issues
                    };

                    _db.AssetTypes.Add(assetType);
                    _db.SaveChanges();

                    TempData["SuccessMessage"] = "Asset Type added successfully!";
                    return RedirectToAction("AddAssetType"); // Redirect to avoid duplicate submissions
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var validationErrors in ex.EntityValidationErrors)
                    {
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            ModelState.AddModelError(validationError.PropertyName, validationError.ErrorMessage);
                        }
                    }
                }
            }

            return View(model);
        }
public ActionResult AddMaterialCategory()
        {
            ViewBag.AssetTypeID = new SelectList(_db.AssetTypes, "AssetTypeID", "AssetType1"); // Populate dropdown

            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MaterialCategoryView(int AssetTypeID, string[] MCategoryNames)
        {
            try
            {
                if (MCategoryNames == null || MCategoryNames.Length == 0)
                {
                    ModelState.AddModelError("", "Please enter at least one material category.");
                    ViewBag.AssetTypeID = new SelectList(_db.AssetTypes, "AssetTypeID", "AssetType1", AssetTypeID);
                    return View();
                }

                // Trim and filter out empty category names
                var validCategories = MCategoryNames
                    .Select(name => name?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase) // Remove duplicate entries from input
                    .ToList();

                if (validCategories.Count == 0)
                {
                    ModelState.AddModelError("", "Please enter valid material categories.");
                    ViewBag.AssetTypeID = new SelectList(_db.AssetTypes, "AssetTypeID", "AssetType1", AssetTypeID);
                    return View();
                }

                // Get existing categories for the selected AssetType
                var existingCategories = _db.MaterialCategories
                    .Where(mc => mc.AssetTypeID == AssetTypeID)
                    .Select(mc => mc.MaterialCategory1.ToLower())
                    .ToHashSet(); // Optimized lookup

                List<string> skippedCategories = new List<string>();

                foreach (var categoryName in validCategories)
                {
                    string lowerCategory = categoryName.ToLower();

                    if (existingCategories.Contains(lowerCategory))
                    {
                        skippedCategories.Add(categoryName);
                        continue;
                    }

                    MaterialCategory newCategory = new MaterialCategory
                    {
                        AssetTypeID = AssetTypeID,
                        MaterialCategory1 = categoryName
                    };

                    _db.MaterialCategories.Add(newCategory);
                }

                _db.SaveChanges();

                if (skippedCategories.Any())
                {
                    TempData["WarningMessage"] = "Some categories were skipped because they already exist: " + string.Join(", ", skippedCategories);
                }

                TempData["SuccessMessage"] = "Material categories added successfully!";
                return RedirectToAction("AddMaterialCategory");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving categories: " + ex.Message);
            }

            ViewBag.AssetTypeID = new SelectList(_db.AssetTypes, "AssetTypeID", "AssetType1", AssetTypeID);
            return View();
        }
    



        public ActionResult AddMaterialSubCategory()
        {
            ViewBag.AssetTypeID = new SelectList(_db.AssetTypes, "AssetTypeID", "AssetType1");
            ViewBag.MID = new SelectList(Enumerable.Empty<SelectListItem>(), "MID", "MCategoryName"); // Initially empty dropdown

            var existingSubcategories = _db.MaterialSubCategories.ToList(); // Fetch existing subcategories
            return View(existingSubcategories);

        }



        [HttpPost]
        public JsonResult MaterialSubCategoryView(int MID, int PurchaseDepartment, List<string> MSubCategoryNames)
        {
            try
            {
                if (MSubCategoryNames == null || MSubCategoryNames.Count == 0)
                {
                    return Json(new { success = false, message = "No subcategories provided." });
                }

                foreach (var subcategoryName in MSubCategoryNames)
                {
                    var newSubCategory = new MaterialSubCategory
                    {
                        MID = MID,
                        //PDNo = PurchaseDepartment,
                        MaterialSubCategory1 = subcategoryName.Trim()
                    };

                    _db.MaterialSubCategories.Add(newSubCategory);
                }

                _db.SaveChanges();

                return Json(new { success = true, message = "Subcategories saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        //Fetch Material Categories based on selected Asset Type(AJAX)
        public JsonResult GetMaterialCategories1(int assetTypeID)
        {
            var categories = _db.MaterialCategories
                .Where(m => m.AssetTypeID == assetTypeID)
                .Select(m => new { MID = m.MID, MCategoryName = m.MaterialCategory1 })
                .ToList();

            return Json(categories, JsonRequestBehavior.AllowGet);
        }

        // Fetch existing Material Subcategories (AJAX)
        public JsonResult GetMaterialSubCategories()
        {
            var subCategories = _db.MaterialSubCategories
                .Select(s => new { s.MID, s.MaterialSubCategory1 })
                .ToList();

            return Json(subCategories, JsonRequestBehavior.AllowGet);
        }



        public ActionResult RaiseRequest(string assetType, string materialCategory, string materialSubCategory, DateTime? fromDate, DateTime? toDate)
        {
            var requests = _db.RequiredMaterials
                              .Where(r => r.Status == "New") // Filter only "New" status
                              .AsQueryable();

            // Ensure ViewBag variables are always initialized
            ViewBag.AssetTypes = _db.RequiredMaterials
                                    .Where(r => r.Status == "New")
                                    .Select(r => r.AssetType)
                                    .Distinct()
                                    .ToList();

            ViewBag.MaterialCategories = _db.RequiredMaterials
                                            .Where(r => r.Status == "New")
                                            .Select(r => r.MaterialCategory)
                                            .Distinct()
                                            .ToList();

            ViewBag.MaterialSubCategories = _db.RequiredMaterials
                                               .Where(r => r.Status == "New")
                                               .Select(r => r.MaterialSubCategory)
                                               .Distinct()
                                               .ToList();

            if (!string.IsNullOrEmpty(assetType))
            {
                requests = requests.Where(r => r.AssetType == assetType);
                ViewBag.SelectedAssetType = assetType;
            }
            if (!string.IsNullOrEmpty(materialCategory))
            {
                requests = requests.Where(r => r.MaterialCategory == materialCategory);
                ViewBag.SelectedMaterialCategory = materialCategory;
            }
            if (!string.IsNullOrEmpty(materialSubCategory))
            {
                requests = requests.Where(r => r.MaterialSubCategory == materialSubCategory);
                ViewBag.SelectedMaterialSubCategory = materialSubCategory;
            }

            // Calculate total requested quantity for "New" status
            int totalRequestedQuantity = requests.Sum(r => (int?)r.RequiredQuantity) ?? 0;
            ViewBag.TotalRequestedQuantity = totalRequestedQuantity;

            var requestList = requests.ToList();
            return View(requestList);
        }







        public ActionResult GetMaterialCategoriesByAssetType(string assetType)
        {
            var categories = _db.RequiredMaterials
                                .Where(r => r.AssetType == assetType)
                                .Select(r => r.MaterialCategory)
                                .Distinct()
                                .ToList();

            return Json(categories, JsonRequestBehavior.AllowGet);
        }



        public ActionResult GetMaterialSubCategories1(string assetType, string materialCategory)
        {
            var subCategories = _db.RequiredMaterials
                                   .Where(r => r.AssetType == assetType && r.MaterialCategory == materialCategory)
                                   .Select(r => r.MaterialSubCategory)
                                   .Distinct()
                                   .ToList();

            return Json(subCategories, JsonRequestBehavior.AllowGet);
        }



        [HttpPost]
        public ActionResult RaiseRequestAction(string AssetType, string MaterialCategory, string MaterialSubCategory, int TotalRequestedQuantity)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("RaiseRequestAction called.");

                if (string.IsNullOrEmpty(AssetType) || string.IsNullOrEmpty(MaterialCategory) || string.IsNullOrEmpty(MaterialSubCategory))
                {
                    ViewBag.ErrorMessage = "Invalid request data.";
                    return RedirectToAction("RaiseRequest");
                }

                System.Diagnostics.Debug.WriteLine($"Request Data - AssetType: {AssetType}, MaterialCategory: {MaterialCategory}, MaterialSubCategory: {MaterialSubCategory}, RequestingQuantity: {TotalRequestedQuantity}");

                int storeAdminID = Convert.ToInt32(Session["UserID"]);
                var storeAdmin = _db.StoreAdmins.FirstOrDefault(s => int.Parse(s.StoreAdminID) == storeAdminID);
                int universityID = storeAdmin?.UniversityID ?? 0;

                var requiredMaterials = _db.RequiredMaterials
                    .Where(r => r.AssetType == AssetType
                             && r.MaterialCategory == MaterialCategory
                             && r.MaterialSubCategory == MaterialSubCategory
                             && r.Status == "New")
                    .ToList();

                if (!requiredMaterials.Any())
                {
                    ViewBag.ErrorMessage = "No pending requests found.";
                    return RedirectToAction("RaiseRequest");
                }

                foreach (var item in requiredMaterials)
                {
                    item.Status = "Raised";
                }
                _db.SaveChanges();

                RequestingMaterial newRequest = new RequestingMaterial
                {
                    AssetType = AssetType,
                    MaterialCategory = MaterialCategory,
                    MaterialSubCategory = MaterialSubCategory,
                    RequestingQuantity = TotalRequestedQuantity,
                    Status = "Raised",
                    RequestedDate = DateTime.Now,
                    StoreAdminID = storeAdminID,
                    UniversityID = universityID,
                    PurchaseDepartmentID = 0,
                    VendorID = 0
                };

                _db.RequestingMaterials.Add(newRequest);
                _db.SaveChanges();

                TempData["SuccessMessage"] = "Request successfully raised!";
                return RedirectToAction("RaiseRequest");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                return RedirectToAction("RaiseRequest");
            }
        }


        //public ActionResult MyRequests()
        //{
        //    int storeAdminID = Convert.ToInt32(Session["UserID"]);
        //    var raisedRequests = _db.RequiredMaterials
        //        .Where(r => r.StoreAdminID == storeAdminID && r.Status == "Raised")
        //        .OrderByDescending(r => r.RequestedDate) // Sort by Requested Date (Latest First)
        //        .ToList();

        //    return View(raisedRequests);
        //}

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (Session["UserID"] == null) // Check if session exists
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(new { controller = "Login", action = "Loginpage" })
                );
            }
            base.OnActionExecuting(filterContext);
        }

        public ActionResult PODetails()
        {
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"].ToString();
            }

            return View(); // just the empty search form initially
        }

        [HttpPost]
        public ActionResult PODetails(string poNumber)
        {
            if (string.IsNullOrEmpty(poNumber))
            {
                ViewBag.Error = "Please enter a PO Number.";
                return View("PODetails");
            }

            var po = _db.PurchaseOrders.FirstOrDefault(p => p.PONumber.ToString() == poNumber);
            if (po == null)
            {
                ViewBag.Error = $"No Purchase Order found with PO Number {poNumber}.";
                return View("PODetails");
            }

            var items = _db.PurchaseOrderItems.Where(i => i.PONumber.ToString() == poNumber).ToList();

        
            var viewModel = new GeneratePOViewModel
            {
                PONumber = po.PONumber.ToString(),
                PODate  = (DateTime)po.PODate,
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
                CopiesOfInvoice = po.CopiesOfInvoice ??0,
                AuthorizedBy = po.AuthorizedBy,
                StoreUploads = po.StoreUploads,
                PurchaseOrderItems = items.Select(item => new PurchaseOrderItem
                
                {
                    POItemID = item.POItemID,
                    QtyOrdered = item.QtyOrdered ?? 0,
                    QtyReceived = item.QtyReceived,
                    Description = item.Description,
                    //UnitPrice = item.UnitPrice ?? 0,
                    //Total = (item.QtyOrdered ?? 0) * (item.UnitPrice ?? 0),
                    Remarks = item.Remarks,
                    AcceptedQty = item.AcceptedQty,
                    RejectedQty = item.RejectedQty,
                    VendorEmail = item.VendorEmail
                }).ToList()
            };
            return View(viewModel);
        }

        [HttpPost]
        public ActionResult UpdatePOItems(GeneratePOViewModel model)
        {
            if (model.PurchaseOrderItems != null && model.PurchaseOrderItems.Count > 0)
            {
                foreach (var item in model.PurchaseOrderItems)
                {
                    var existingItem = _db.PurchaseOrderItems.FirstOrDefault(p => p.POItemID == item.POItemID);

                    if (existingItem != null)
                    {
                        existingItem.QtyReceived = item.QtyReceived;
                        existingItem.Remarks = item.Remarks;
                        existingItem.RejectedQty = item.RejectedQty;
                        existingItem.AcceptedQty = item.AcceptedQty;

                        // Update TotalCost based on ReceivedQty * UnitPrice
                        existingItem.Total = (item.AcceptedQty) * (existingItem.UnitPrice);

                        // ✅ Update AvailableQuantity for each unique material in the PO
                        var material = _db.MaterialMasterLists
                            .FirstOrDefault(m => m.MaterialSubCategory == existingItem.Description);

                        if (material != null && item.AcceptedQty.HasValue)
                        {
                            material.AvailableQuantity += item.AcceptedQty.Value;
                            material.IsLowStockAlertSent = false; // allow future alerts
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Updated Material: {material.MaterialSubCategory}, New AvailableQty: {material.AvailableQuantity}");
                        }
                    }

                    else
                    {
                        // Log or debug to make sure this is not null
                        System.Diagnostics.Debug.WriteLine($"POItemID {item.POItemID} not found!");
                    }
                    // ✅ Update the PO status to Delivered
                    var po = _db.PurchaseOrders.FirstOrDefault(p => p.PONumber.ToString() == model.PONumber);
                    if (po != null)
                    {
                        po.Status = "Delivered";
                    }
                }

                // ✅ Save certification file
                // Save Certification file
                if (model.CertificationFile != null && model.CertificationFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileName(model.CertificationFile.FileName);
                    string path = Path.Combine(Server.MapPath("~/UploadedCertificates/"), fileName);
                    model.CertificationFile.SaveAs(path);
                    var po = _db.PurchaseOrders.FirstOrDefault(p => p.PONumber.ToString() == model.PONumber);

                    if (po != null)
                    {
                        po.StoreUploads = "/UploadedCertificates/" + fileName;
                    }
                }


                foreach (var item in model.PurchaseOrderItems)
                {
                    System.Diagnostics.Debug.WriteLine($"POItemID: {item.POItemID}, Received: {item.QtyReceived}, Remarks: {item.Remarks},Accepted: {item.AcceptedQty},Rejected: {item.RejectedQty}");
                }

                _db.SaveChanges();
                TempData["SuccessMessage"] = "Purchase Order items updated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "No items received to update.";
            }

            return RedirectToAction("PODetails", new { poNumber = model.PONumber });
        }

        public ActionResult CentralPODetails()
        {
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"].ToString();
            }

            return View(); // just the empty search form initially
        }

        [HttpPost]
        public ActionResult CentralPODetails(string poNumber)
        {
            if (string.IsNullOrEmpty(poNumber))
            {
                ViewBag.Error = "Please enter a PO Number.";
                return View("PODetails");
            }

            var po = _db.CentralPurchaseOrders.FirstOrDefault(p => p.PONumber.ToString() == poNumber);
            if (po == null)
            {
                ViewBag.Error = $"No Purchase Order found with PO Number {poNumber}.";
                return View("PODetails");
            }

            var items = _db.CentralPurchaseOrderItems.Where(i => i.PONumber.ToString() == poNumber).ToList();


            var viewModel = new CentralGeneratePOViewModel
            {
                PONumber = po.PONumber.ToString(),
                PODate = (DateTime)po.PODate,
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
                CopiesOfInvoice = po.CopiesOfInvoice ?? 0,
                AuthorizedBy = po.AuthorizedBy,
                StoreUploads = po.StoreUploads,

                CentralPurchaseOrderItems = items.Select(item => new CentralPurchaseOrderItem
                {
                    POItemID = item.POItemID,
                    QtyOrdered = item.QtyOrdered ?? 0,
                    QtyReceived = item.QtyReceived,
                    AcceptedQty = item.AcceptedQty,
                    RejectedQty = item.RejectedQty,
                    Description = item.Description,
                    //UnitPrice = item.UnitPrice ?? 0,
                    //Total = (item.QtyOrdered ?? 0) * (item.UnitPrice ?? 0),
                    Remarks = item.Remarks,
                    VendorEmail = item.VendorEmail
                }).ToList()
            };
            return View(viewModel);
        }

        [HttpPost]
        public ActionResult CentralUpdatePOItems(CentralGeneratePOViewModel model)
        {
            if (model.CentralPurchaseOrderItems != null && model.CentralPurchaseOrderItems.Count > 0)
            {
                foreach (var item in model.CentralPurchaseOrderItems)
                {
                    var existingItem = _db.CentralPurchaseOrderItems.FirstOrDefault(p => p.POItemID == item.POItemID);

                    if (existingItem != null)
                    {
                        existingItem.QtyReceived = item.QtyReceived;
                        existingItem.Remarks = item.Remarks;
                        existingItem.RejectedQty = item.RejectedQty;
                        existingItem.AcceptedQty = item.AcceptedQty;


                        // Update TotalCost based on ReceivedQty * UnitPrice
                        existingItem.Total = (item.AcceptedQty ?? 0) * (existingItem.UnitPrice ?? 0);

                        // ✅ Update AvailableQuantity for each unique material in the PO
                        var material = _db.MaterialMasterLists
                            .FirstOrDefault(m => m.MaterialSubCategory == existingItem.Description);

                        if (material != null && item.AcceptedQty.HasValue)
                        {
                            material.AvailableQuantity += item.AcceptedQty.Value;
                            material.IsLowStockAlertSent = false; // allow future alerts
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Updated Material: {material.MaterialSubCategory}, New AvailableQty: {material.AvailableQuantity}Accepted: {item.AcceptedQty},Rejected: {item.RejectedQty}");
                        }
                    }
                    else
                    {
                        // Log or debug to make sure this is not null
                        System.Diagnostics.Debug.WriteLine($"POItemID {item.POItemID} not found!");
                    }
                    // ✅ Update the PO status to Delivered
                    var po = _db.CentralPurchaseOrders.FirstOrDefault(p => p.PONumber.ToString() == model.PONumber);
                    if (po != null)
                    {
                        po.Status = "Delivered";
                    }

                }
                    // ✅ Save certification file
                    // Save Certification file
                    if (model.CertificationFile != null && model.CertificationFile.ContentLength > 0)
                    {
                        string fileName = Path.GetFileName(model.CertificationFile.FileName);
                        string path = Path.Combine(Server.MapPath("~/UploadedCertificates/"), fileName);
                        model.CertificationFile.SaveAs(path);
                        var po = _db.CentralPurchaseOrders.FirstOrDefault(p => p.PONumber.ToString() == model.PONumber);

                        if (po != null)
                        {
                            po.StoreUploads = "/UploadedCertificates/" + fileName;
                        }
                    }
                

                foreach (var item in model.CentralPurchaseOrderItems)
                {
                    System.Diagnostics.Debug.WriteLine($"POItemID: {item.POItemID}, Received: {item.QtyReceived}, Remarks: {item.Remarks}");
                }

                _db.SaveChanges();
                TempData["SuccessMessage"] = "Purchase Order items updated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "No items received to update.";
            }

            return RedirectToAction("CentralPODetails", new { poNumber = model.PONumber });
        }

        [HttpGet]
        public ActionResult Report(DateTime? startDate, DateTime? endDate)
        {
            if (startDate == null || endDate == null)
            {
                TempData["Error"] = "Please select both start and end dates.";
                return View(new ReportViewModel()); // empty model
            }

            DateTime fromDate = startDate.Value.Date;
            DateTime toDate = endDate.Value.Date.AddDays(1).AddSeconds(-1);

            // Fetch detailed data
            var employeeData = _db.EmployeeIssueMaterials
                .Where(e => e.IssuedDate >= fromDate && e.IssuedDate <= toDate)
                .Select(e => new MaterialIssueReportViewModel
                {
                    MaterialName = e.MaterialSubCategory,
                    IssuedDate = e.IssuedDate,
                    RequestedQuantity = e.RequestingQuantity,
                    IssuedQuantity = e.IssuingQuantity,
                    ClosingQuantity = e.ClosingQuantity,
                    IssuedTo = e.EmpID,
                    Role= "Employee"
                });

            var hodData = _db.HODIssueMaterials
                .Where(h => h.IssuedDate >= fromDate && h.IssuedDate <= toDate)
                .Select(h => new MaterialIssueReportViewModel
                {
                    MaterialName = h.MaterialSubCategory,
                    IssuedDate = (DateTime)h.IssuedDate,
                    RequestedQuantity = h.RequestingQuantity ?? 0,
                    IssuedQuantity = h.IssuingQuantity ?? 0,
                    ClosingQuantity = h.ClosingQuantity ?? 0,
                    IssuedTo = h.HODID,
                    Role ="HOD"
                });

            var detailedList = employeeData
                .Concat(hodData)
                .OrderBy(x => x.IssuedDate)
                .ThenBy(x => x.MaterialName)
                .ToList();

            // Generate summary
            var summaryList = detailedList
                .GroupBy(x => x.MaterialName)
                .Select(g => new MaterialSummaryViewModel
                {
                    MaterialName = g.Key,
                    TotalIssuedQuantity = g.Sum(x => x.IssuedQuantity),
                    ClosingQuantity = g.OrderByDescending(x => x.IssuedDate).First().ClosingQuantity
                })
                .OrderBy(x => x.MaterialName)
                .ToList();

            var model = new ReportViewModel
            {
                DetailedReports = detailedList,
                SummaryReports = summaryList
            };

            return View(model);
        }

        //public ActionResult MRV()
        //{
        //    return View();
        //}

        public ActionResult ExpiredMaterials()
        {
            using (var db = new ASPEntities2())
            {
                DateTime today = DateTime.Today;

                var expiredMaterials = db.MaterialMasterLists
                    .Where(m => m.ExpiryDate != null && m.ExpiryDate < today)
                    .ToList();

                return View(expiredMaterials);
            }
        }


    }
}