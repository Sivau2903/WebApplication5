using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace WebApplication5.Controllers
{
    public class CentralPurchaseDepartment : Controller
    {
        // GET: CentralPurchaseDepartment
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

        public ActionResult CPDDashBoard()
        {
            return View();
        }
        public ActionResult RequestsRecevied()
        {
            return View();
        }
        public ActionResult MyRequests()
        {
            return View();
        }
    }
}