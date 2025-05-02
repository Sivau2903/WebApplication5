using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace WebApplication5.Controllers
{
    public class BaseController : Controller
    {
        // GET: Base

            protected override void OnActionExecuting(ActionExecutingContext filterContext)
            {
                // Get current controller and action names
                var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
                var actionName = filterContext.ActionDescriptor.ActionName;

                // Allow specific pages without login
                var allowAnonymousActions = new[]
                {
            new { Controller = "Login", Action = "LoginPage" },
            new { Controller = "Login", Action = "Authenticate" },
            new { Controller = "Login", Action = "ForgotPassword" },
            new { Controller = "Login", Action = "VerifyOTP" },
            new { Controller = "Login", Action = "ResetPasswordRequest" },
            new { Controller = "Login", Action = "ResetPassword" }
        };

                bool isAnonymous = allowAnonymousActions.Any(a =>
                    string.Equals(a.Controller, controllerName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(a.Action, actionName, StringComparison.OrdinalIgnoreCase)
                );

                if (!isAnonymous && Session["UserID"] == null)
                {
                    filterContext.Result = new RedirectToRouteResult(
                        new RouteValueDictionary {
                    { "controller", "Login" },
                    { "action", "LoginPage" }
                        });
                    return;
                }

                // Prevent caching for back/forward navigation
                Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
                Response.Cache.SetValidUntilExpires(false);
                Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();

                base.OnActionExecuting(filterContext);
            }
        }

    }
