using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebApplication5.Models;

namespace WebApplication5.Controllers
{
    public class SummarizerController : Controller
    {
        private readonly ASPEntities2 _db = new ASPEntities2();
        // GET: Summarizer
        public ActionResult Home()
        {
            return View();
        }

        public ActionResult AllPOs()
        {
            var draftPOs = _db.CentralPurchaseOrders
                              .Where(po => po.Status == "Draft")
                              .OrderByDescending(po => po.PODate)
                              .ToList();

            var deliveredPOs = _db.CentralPurchaseOrders
                                  .Where(po => po.Status == "Delivered")
                                  .OrderByDescending(po => po.PODate)
                                  .ToList();

            ViewBag.DraftPOs = draftPOs;
            ViewBag.DeliveredPOs = deliveredPOs;

            return View();
        }


        [HttpGet]
        public ActionResult PODetails(string poNumber)
        {
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"].ToString();
            }

            if (string.IsNullOrEmpty(poNumber))
            {
                return View(); // return empty if no PO number is passed
            }

            var po = _db.CentralPurchaseOrders.FirstOrDefault(p => p.PONumber.ToString() == poNumber);
            if (po == null)
            {
                ViewBag.Error = $"No Purchase Order found with PO Number {poNumber}.";
                return View();
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
                PurchaseDepartmentUploads = po.PurchaseDepartmentUploads,

                CentralPurchaseOrderItems = items.Select(item => new CentralPurchaseOrderItem
                {
                    POItemID = item.POItemID,
                    QtyOrdered = item.QtyOrdered ?? 0,
                    QtyReceived = item.QtyReceived,
                    AcceptedQty = item.AcceptedQty,
                    RejectedQty = item.RejectedQty,
                    Description = item.Description,
                    UnitPrice = item.UnitPrice ?? 0,
                    Total = (item.QtyOrdered ?? 0) * (item.UnitPrice ?? 0),
                    Remarks = item.Remarks,
                    VendorEmail = item.VendorEmail
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        public ActionResult InitiatePayment(CentralGeneratePOViewModel model)
        {
            var userID = Session["UserID"]?.ToString();

            if (string.IsNullOrEmpty(userID))
            {
                TempData["ErrorMessage"] = "User session expired. Please login again.";
                return RedirectToAction("LoginPage", "Login");
            }

            var po = _db.CentralPurchaseOrders.FirstOrDefault(p => p.PONumber.ToString() == model.PONumber);

            if (po == null)
            {
                TempData["ErrorMessage"] = "Purchase Order not found.";
                return RedirectToAction("AllPOs");
            }

            po.Statement = model.Statement;
            po.Status = "Sent to Accountant";
            po.SummarizerID = userID;
            po.CentralAccountantID = "IURCACC1";

            _db.SaveChanges();

            TempData["SuccessMessage"] = "Purchase Order successfully sent to Central Accountant.";
            return RedirectToAction("AllPOs");
        }



    }
}