using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebApplication5.Models;

namespace WebApplication5.Controllers
{
    public class AuditorController : Controller
    {
        private readonly ASPEntities2 _db = new ASPEntities2();

        // GET: Auditor
        public ActionResult Home()
        {
            return View();
        }

        public ActionResult RequestsRecevied()
        {
            string purchaseDepartmentID = (string)Session["UserID"];

            var raisedRequests = (from request in _db.SavetoCentrals
                                  where request.AuditorID == purchaseDepartmentID &&
                                        (request.Status == "Sent to Auditor")
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



        [HttpPost]
        public ActionResult UpdateOrderQuantities( List<OrderUpdateModel> updates)
        {
            if (updates == null || !updates.Any())
                return Json(new { success = false, message = "No data received." });

            
                foreach (var update in updates)
                {
                    var request = _db.SavetoCentrals.FirstOrDefault(r => r.ID == update.ID);
                    if (request != null)
                    {
                        request.Order_Quantity = update.OrderQuantity;
                    request.Status = "Sent to Central";
                    request.CentralID = "IURCPD1";

                        // Optionally, update other fields or audit info here
                    }
                }

                _db.SaveChanges();
            

            return Json(new { success = true });
        }





        public JsonResult GetAvailableBudget()
        {
            string sessionID = (string)Session["UserID"]; // Adjust based on your session handling
            var budget = _db.CentralAuditors
                            .Where(lpd => lpd.AuditorID == sessionID)
                            .Select(lpd => lpd.Budget)
                            .FirstOrDefault();

            return Json(budget, JsonRequestBehavior.AllowGet);
        }

    }
}