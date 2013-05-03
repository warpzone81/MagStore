﻿using System;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using MagStore.Entities;
using MagStore.Infrastructure;
using MagStore.Infrastructure.Interfaces;
using MagStore.Provider;
using MagStore.Web.Models;
using MagStore.Web.Models.Account;

namespace MagStore.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IRepository finder;
        private IFormsAuthenticationService FormsService { get; set; }
        private IMembershipService MembershipService { get; set; }
        private RoleProvider roleProvider;

        public AccountController(IRepository finder, RoleProvider roleProvider)
        {
            this.finder = finder;
            this.roleProvider = roleProvider;
        }

        protected override void Initialize(RequestContext requestContext)
        {
            if (FormsService == null) { FormsService = new FormsAuthenticationService(); }
            if (MembershipService == null) { MembershipService = new AccountMembershipService(); }

            base.Initialize(requestContext);
        }

        // **************************************
        // URL: /Account/LogOn
        // **************************************

        public ActionResult LogOn()
        {
            return View();
        }

        [HttpPost]
        public ActionResult LogOn(LogOnModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                if (MembershipService.ValidateUser(model.UserName, model.Password))
                {
                    FormsService.SignIn(model.UserName, model.RememberMe);
                    var providerUserKey = MembershipService.GetUser(model.UserName).ProviderUserKey as string;
                    var currentUser = finder.Load<User>(providerUserKey);
                    Session["CurrentUser"] = currentUser;
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError("", "The user name or password provided is incorrect.");
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        // **************************************
        // URL: /Account/LogOff
        // **************************************

        public ActionResult LogOff()
        {
            FormsService.SignOut();

            return RedirectToAction("Index", "Home");
        }

        // **************************************
        // URL: /Account/Register
        // **************************************

        public ActionResult Register()
        {
            ViewBag.PasswordLength = MembershipService.MinPasswordLength;
            return View();
        }

        [HttpPost]
        public ActionResult Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                // Attempt to register the user
                var createStatus = MembershipService.CreateUser(model.UserName, model.Password, model.Email);

                if (createStatus == MembershipCreateStatus.Success)
                {
                    FormsService.SignIn(model.UserName, false /* createPersistentCookie */);
                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError("", AccountValidation.ErrorCodeToString(createStatus));
            }

            // If we got this far, something failed, redisplay form
            ViewBag.PasswordLength = MembershipService.MinPasswordLength;
            return View(model);
        }

        // **************************************
        // URL: /Account/ChangePassword
        // **************************************

        [Authorize]
        public ActionResult ChangePassword()
        {
            ViewBag.PasswordLength = MembershipService.MinPasswordLength;
            return View();
        }

        [Authorize]
        [HttpPost]
        public ActionResult ChangePassword(ChangePasswordModel model)
        {
            if (ModelState.IsValid)
            {
                if (MembershipService.ChangePassword(User.Identity.Name, model.OldPassword, model.NewPassword))
                {
                    return RedirectToAction("ChangePasswordSuccess");
                }
                ModelState.AddModelError("", "The current password is incorrect or the new password is invalid.");
            }

            // If we got this far, something failed, redisplay form
            ViewBag.PasswordLength = MembershipService.MinPasswordLength;
            return View(model);
        }

        // **************************************
        // URL: /Account/ChangePasswordSuccess
        // **************************************

        public ActionResult ChangePasswordSuccess()
        {
            return View();
        }

        [Authorize(Roles="Administrator")]
        public ActionResult ManageUsers()
        {
            var users = MembershipService.GetAllUsers();
            return View(users);
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult ManageRoles()
        {
            var roles = MembershipService.GetAllRoles();
            return View(roles);
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public ActionResult ManageRoles(string roleName)
        {
            if (String.IsNullOrEmpty(roleName))
            {
                ModelState.AddModelError("roleName", "Name is required");
            }
            else
            {
                MembershipService.AddRole(roleName);
            }
            return RedirectToAction("ManageRoles");
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult EditUser()
        {
            var currentUser = System.Web.HttpContext.Current.Session["CurrentUser"] as User;
            if (currentUser != null)
            {
                var user = MembershipService.GetUser(currentUser.Username);
                var roles = MembershipService.GetAllRoles();
                var userRoles = MembershipService.GetRolesForUser(user.UserName);

                return View(new EditUserModel(user.UserName, user.Email, roles, userRoles));
            }
            return View("Error", new HandleErrorInfo(new Exception("CurrentUser is null"), "Account", "EditUser"));
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public ActionResult EditUser(EditUserModel model)
        {
            var user = MembershipService.GetUser(model.Username);
            MembershipService.UpdateUser(user, model.UserRoles);
            return RedirectToAction("ManageUsers");
        }

        [HttpGet]
        public ActionResult CreateRole()
        {
            User u = Session["CurrentUser"] as User;
            var viewModel = new CreateRoleViewModel
            {
                User = u
            };
            return View(viewModel);
        }

        [HttpPost]
        public ActionResult CreateRole(CreateRoleInputModel inputModel)
        {
            roleProvider.CreateRole(inputModel.Role);
            return RedirectToAction("CreateRole");
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public ActionResult DeleteRole(string roleName)
        {
            MembershipService.DeleteRole(roleName);
            return RedirectToAction("ManageRoles");
        }
    }

//    [Authorize]
//    public class AccountController : Controller
//    {
//        private readonly IShop shop;
//
//        public AccountController(IShop shop)
//        {
//            this.shop = shop;
//        }
//
//        //
//        // GET: /Account/Login
//
//        [AllowAnonymous]
//        public ActionResult Login(string returnUrl)
//        {
//            ViewBag.ReturnUrl = returnUrl;
//            return View();
//        }
//
//        //
//        // POST: /Account/Login
//
//        [HttpPost]
//        [AllowAnonymous]
//        [ValidateAntiForgeryToken]
//        public ActionResult Login(LoginModel model, string returnUrl)
//        {
//            if (ModelState.IsValid && WebSecurity.Login(model.UserName, model.Password, persistCookie: model.RememberMe))
//            {
//                return RedirectToLocal(returnUrl);
//            }
//
//            // If we got this far, something failed, redisplay form
//            ModelState.AddModelError("", "The user name or password provided is incorrect.");
//            return View(model);
//        }
//
//        //
//        // POST: /Account/LogOff
//
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public ActionResult LogOff()
//        {
//            WebSecurity.Logout();
//
//            return RedirectToAction("Index", "Home");
//        }
//
//        //
//        // GET: /Account/Register
//
//        [AllowAnonymous]
//        public ActionResult Register()
//        {
//            return View();
//        }
//
//        //
//        // POST: /Account/Register
//
//        [HttpPost]
//        [AllowAnonymous]
//        [ValidateAntiForgeryToken]
//        public ActionResult Register(RegisterModel model)
//        {
//            if (ModelState.IsValid)
//            {
//                // Attempt to register the user
//                try
//                {
//                    WebSecurity.CreateUserAndAccount(model.UserName, model.Password);
//                    WebSecurity.Login(model.UserName, model.Password);
//                    return RedirectToAction("Index", "Home");
//                }
//                catch (MembershipCreateUserException e)
//                {
//                    ModelState.AddModelError("", ErrorCodeToString(e.StatusCode));
//                }
//            }
//
//            // If we got this far, something failed, redisplay form
//            return View(model);
//        }
//
//        //
//        // POST: /Account/Disassociate
//
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public ActionResult Disassociate(string provider, string providerUserId)
//        {
//            string ownerAccount = OAuthWebSecurity.GetUserName(provider, providerUserId);
//            ManageMessageId? message = null;
//
//            // Only disassociate the account if the currently logged in user is the owner
//            if (ownerAccount == User.Identity.Name)
//            {
//                // Use a transaction to prevent the user from deleting their last login credential
//                using (var scope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.Serializable }))
//                {
//                    bool hasLocalAccount = OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
//                    if (hasLocalAccount || OAuthWebSecurity.GetAccountsFromUserName(User.Identity.Name).Count > 1)
//                    {
//                        OAuthWebSecurity.DeleteAccount(provider, providerUserId);
//                        scope.Complete();
//                        message = ManageMessageId.RemoveLoginSuccess;
//                    }
//                }
//            }
//
//            return RedirectToAction("Manage", new { Message = message });
//        }
//
//        //
//        // GET: /Account/Manage
//
//        public ActionResult Manage(ManageMessageId? message)
//        {
//            ViewBag.StatusMessage =
//                message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
//                : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
//                : message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
//                : "";
//            ViewBag.HasLocalPassword = OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
//            ViewBag.ReturnUrl = Url.Action("Manage");
//            return View();
//        }
//
//        //
//        // POST: /Account/Manage
//
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public ActionResult Manage(LocalPasswordModel model)
//        {
//            bool hasLocalAccount = OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
//            ViewBag.HasLocalPassword = hasLocalAccount;
//            ViewBag.ReturnUrl = Url.Action("Manage");
//            if (hasLocalAccount)
//            {
//                if (ModelState.IsValid)
//                {
//                    // ChangePassword will throw an exception rather than return false in certain failure scenarios.
//                    bool changePasswordSucceeded;
//                    try
//                    {
//                        changePasswordSucceeded = WebSecurity.ChangePassword(User.Identity.Name, model.OldPassword, model.NewPassword);
//                    }
//                    catch (Exception)
//                    {
//                        changePasswordSucceeded = false;
//                    }
//
//                    if (changePasswordSucceeded)
//                    {
//                        return RedirectToAction("Manage", new { Message = ManageMessageId.ChangePasswordSuccess });
//                    }
//                    else
//                    {
//                        ModelState.AddModelError("", "The current password is incorrect or the new password is invalid.");
//                    }
//                }
//            }
//            else
//            {
//                // User does not have a local password so remove any validation errors caused by a missing
//                // OldPassword field
//                ModelState state = ModelState["OldPassword"];
//                if (state != null)
//                {
//                    state.Errors.Clear();
//                }
//
//                if (ModelState.IsValid)
//                {
//                    try
//                    {
//                        WebSecurity.CreateAccount(User.Identity.Name, model.NewPassword);
//                        return RedirectToAction("Manage", new { Message = ManageMessageId.SetPasswordSuccess });
//                    }
//                    catch (Exception e)
//                    {
//                        ModelState.AddModelError("", e);
//                    }
//                }
//            }
//
//            // If we got this far, something failed, redisplay form
//            return View(model);
//        }
//
//        //
//        // POST: /Account/ExternalLogin
//
//        [HttpPost]
//        [AllowAnonymous]
//        [ValidateAntiForgeryToken]
//        public ActionResult ExternalLogin(string provider, string returnUrl)
//        {
//            return new ExternalLoginResult(provider, Url.Action("ExternalLoginCallback", new { ReturnUrl = returnUrl }));
//        }
//
//        //
//        // GET: /Account/ExternalLoginCallback
//
//        [AllowAnonymous]
//        public ActionResult ExternalLoginCallback(string returnUrl)
//        {
//            AuthenticationResult result = OAuthWebSecurity.VerifyAuthentication(Url.Action("ExternalLoginCallback", new { ReturnUrl = returnUrl }));
//            if (!result.IsSuccessful)
//            {
//                return RedirectToAction("ExternalLoginFailure");
//            }
//
//            if (OAuthWebSecurity.Login(result.Provider, result.ProviderUserId, createPersistentCookie: false))
//            {
//                return RedirectToLocal(returnUrl);
//            }
//
//            if (User.Identity.IsAuthenticated)
//            {
//                // If the current user is logged in add the new account
//                OAuthWebSecurity.CreateOrUpdateAccount(result.Provider, result.ProviderUserId, User.Identity.Name);
//                return RedirectToLocal(returnUrl);
//            }
//            else
//            {
//                // User is new, ask for their desired membership name
//                string loginData = OAuthWebSecurity.SerializeProviderUserId(result.Provider, result.ProviderUserId);
//                ViewBag.ProviderDisplayName = OAuthWebSecurity.GetOAuthClientData(result.Provider).DisplayName;
//                ViewBag.ReturnUrl = returnUrl;
//                return View("ExternalLoginConfirmation", new RegisterExternalLoginModel { UserName = result.UserName, ExternalLoginData = loginData });
//            }
//        }
//
//        //
//        // POST: /Account/ExternalLoginConfirmation
//
//        [HttpPost]
//        [AllowAnonymous]
//        [ValidateAntiForgeryToken]
//        public ActionResult ExternalLoginConfirmation(RegisterExternalLoginModel model, string returnUrl)
//        {
//            string provider = null;
//            string providerUserId = null;
//
//            if (User.Identity.IsAuthenticated || !OAuthWebSecurity.TryDeserializeProviderUserId(model.ExternalLoginData, out provider, out providerUserId))
//            {
//                return RedirectToAction("Manage");
//            }
//
//            if (ModelState.IsValid)
//            {
//                // Insert a new user into the database
//                using (UsersContext db = new UsersContext())
//                {
//                    UserProfile user = db.UserProfiles.FirstOrDefault(u => u.UserName.ToLower() == model.UserName.ToLower());
//                    // Check if user already exists
//                    if (user == null)
//                    {
//                        // Insert name into the profile table
//                        db.UserProfiles.Add(new UserProfile { UserName = model.UserName });
//                        db.SaveChanges();
//
//                        OAuthWebSecurity.CreateOrUpdateAccount(provider, providerUserId, model.UserName);
//                        OAuthWebSecurity.Login(provider, providerUserId, createPersistentCookie: false);
//
//                        return RedirectToLocal(returnUrl);
//                    }
//                    else
//                    {
//                        ModelState.AddModelError("UserName", "User name already exists. Please enter a different user name.");
//                    }
//                }
//            }
//
//            ViewBag.ProviderDisplayName = OAuthWebSecurity.GetOAuthClientData(provider).DisplayName;
//            ViewBag.ReturnUrl = returnUrl;
//            return View(model);
//        }
//
//        //
//        // GET: /Account/ExternalLoginFailure
//
//        [AllowAnonymous]
//        public ActionResult ExternalLoginFailure()
//        {
//            return View();
//        }
//
//        [AllowAnonymous]
//        [ChildActionOnly]
//        public ActionResult ExternalLoginsList(string returnUrl)
//        {
//            ViewBag.ReturnUrl = returnUrl;
//            return PartialView("_ExternalLoginsListPartial", OAuthWebSecurity.RegisteredClientData);
//        }
//
//        [ChildActionOnly]
//        public ActionResult RemoveExternalLogins()
//        {
//            ICollection<OAuthAccount> accounts = OAuthWebSecurity.GetAccountsFromUserName(User.Identity.Name);
//            Project<ExternalLogin> externalLogins = new Project<ExternalLogin>();
//            foreach (OAuthAccount account in accounts)
//            {
//                AuthenticationClientData clientData = OAuthWebSecurity.GetOAuthClientData(account.Provider);
//
//                externalLogins.Add(new ExternalLogin
//                {
//                    Provider = account.Provider,
//                    ProviderDisplayName = clientData.DisplayName,
//                    ProviderUserId = account.ProviderUserId,
//                });
//            }
//
//            ViewBag.ShowRemoveButton = externalLogins.Count > 1 || OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
//            return PartialView("_RemoveExternalLoginsPartial", externalLogins);
//        }
//
//        #region Helpers
//        private ActionResult RedirectToLocal(string returnUrl)
//        {
//            if (Url.IsLocalUrl(returnUrl))
//            {
//                return Redirect(returnUrl);
//            }
//            else
//            {
//                return RedirectToAction("Index", "Home");
//            }
//        }
//
//        public enum ManageMessageId
//        {
//            ChangePasswordSuccess,
//            SetPasswordSuccess,
//            RemoveLoginSuccess,
//        }
//
//        internal class ExternalLoginResult : ActionResult
//        {
//            public ExternalLoginResult(string provider, string returnUrl)
//            {
//                Provider = provider;
//                ReturnUrl = returnUrl;
//            }
//
//            public string Provider { get; private set; }
//            public string ReturnUrl { get; private set; }
//
//            public override void ExecuteResult(ControllerContext context)
//            {
//                OAuthWebSecurity.RequestAuthentication(Provider, ReturnUrl);
//            }
//        }
//
//        private static string ErrorCodeToString(MembershipCreateStatus createStatus)
//        {
//            // See http://go.microsoft.com/fwlink/?LinkID=177550 for
//            // a full list of status codes.
//            switch (createStatus)
//            {
//                case MembershipCreateStatus.DuplicateUserName:
//                    return "User name already exists. Please enter a different user name.";
//
//                case MembershipCreateStatus.DuplicateEmail:
//                    return "A user name for that e-mail address already exists. Please enter a different e-mail address.";
//
//                case MembershipCreateStatus.InvalidPassword:
//                    return "The password provided is invalid. Please enter a valid password value.";
//
//                case MembershipCreateStatus.InvalidEmail:
//                    return "The e-mail address provided is invalid. Please check the value and try again.";
//
//                case MembershipCreateStatus.InvalidAnswer:
//                    return "The password retrieval answer provided is invalid. Please check the value and try again.";
//
//                case MembershipCreateStatus.InvalidQuestion:
//                    return "The password retrieval question provided is invalid. Please check the value and try again.";
//
//                case MembershipCreateStatus.InvalidUserName:
//                    return "The user name provided is invalid. Please check the value and try again.";
//
//                case MembershipCreateStatus.ProviderError:
//                    return "The authentication provider returned an error. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
//
//                case MembershipCreateStatus.UserRejected:
//                    return "The user creation request has been canceled. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
//
//                default:
//                    return "An unknown error occurred. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
//            }
//        }
//        #endregion
//    }
}
