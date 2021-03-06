﻿using System;
using System.Collections.Specialized;
using System.Configuration.Provider;
using System.Linq;
using System.Web.Security;
using MagStore.Entities;
using Microsoft.Practices.ServiceLocation;
using Raven.Abstractions.Exceptions;
using Raven.Client;

namespace MagStore.Provider
{
    public class RavenDbMembershipProvider : MembershipProviderValidated
    {
        private string providerName = "RavenDBMembership";
        private IDocumentStore documentStore;
        private int minRequiredPasswordLength = 7;

        protected IDocumentStore DocumentStore
        {
            get 
            {
                if (documentStore == null)
                {
                    throw new NullReferenceException("The DocumentStore is not set. Please set the DocumentStore or make sure that the Common Service Locator can find the IDocumentStore and call Initialize on this provider.");
                }
                return documentStore;
            }
            set { documentStore = value; }
        }

        public override string ApplicationName
        {
            get; set;
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config.Keys.Cast<string>().Contains("minRequiredPasswordLength"))
            {
                minRequiredPasswordLength = int.Parse(config["minRequiredPasswordLength"]);
            }
            
            // Try to find an IDocumentStore via Common Service Locator. 
            try
            {
                var locator = ServiceLocator.Current;
                if (locator != null)
                {
                    DocumentStore = locator.GetInstance<IDocumentStore>();
                }
            }
            catch (NullReferenceException) // Swallow Nullreference expection that occurs when there is no current service locator.
            {
            }

            providerName = name;
            
            base.Initialize(name, config);
        }

        public override bool CheckedChangePassword(string username, string oldPassword, string newPassword)
        {
            using (var session = DocumentStore.OpenSession())
            {
                var q = from u in session.Query<User>()
                        where u.Username == username && u.ApplicationName == ApplicationName
                        select u;
                var user = q.SingleOrDefault();
                if (user == null || user.PasswordHash != PasswordUtil.HashPassword(oldPassword, user.PasswordSalt))
                {
                    throw new MembershipPasswordException("Invalid username or old password.");
                }

                user.PasswordSalt = PasswordUtil.CreateRandomSalt();
                user.PasswordHash = PasswordUtil.HashPassword(newPassword, user.PasswordSalt);

                session.SaveChanges();
            }
            return true;
        }

        public override bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            throw new NotImplementedException();
        }

        public override MembershipUser CheckedCreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            if (password.Length < MinRequiredPasswordLength)
                throw new MembershipCreateUserException(MembershipCreateStatus.InvalidPassword);

            var args = new ValidatePasswordEventArgs(username, password, true);
            OnValidatingPassword(args);
            if (args.Cancel)
            {
                status = MembershipCreateStatus.InvalidPassword;
                return null;
            }

            var user = new User {Username = username};
            password = password.Trim();
            user.PasswordSalt = PasswordUtil.CreateRandomSalt();
            user.PasswordHash = PasswordUtil.HashPassword(password, user.PasswordSalt);
            user.Email = email;
            user.ApplicationName = ApplicationName;
            user.DateCreated = DateTime.Now;

            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                try
                {
                    session.Store(user);
                    session.Store(new ReservationForUniqueFieldValue { Id = "username/" + user.Username });
                    session.Store(new ReservationForUniqueFieldValue { Id = "email/" + user.Email });

                    session.SaveChanges();

                    status = MembershipCreateStatus.Success;

                    return new MembershipUser(providerName, username, user.Id, email, null, null, true, false, user.DateCreated,
                        new DateTime(1900, 1, 1), new DateTime(1900, 1, 1), DateTime.Now, new DateTime(1900, 1, 1));
                }
                catch (ConcurrencyException e)
                {
                    status = InterpretConcurrencyException(user.Username, user.Email, e);
                }
                catch (Exception ex)
                {
                    // TODO: log exception properly
                    Console.WriteLine(ex.ToString());
                    status = MembershipCreateStatus.ProviderError;
                }
            }
            return null;
        }

        MembershipCreateStatus InterpretConcurrencyException(string username, string email, ConcurrencyException e)
        {
            MembershipCreateStatus status;
            if (e.Message.Contains("username/" + username))
                status = MembershipCreateStatus.DuplicateUserName;
            else if (e.Message.Contains("email/" + email))
                status = MembershipCreateStatus.DuplicateEmail;
            else
            {
                status = MembershipCreateStatus.ProviderError;
            }
            return status;
        }

        public override bool CheckedDeleteUser(string username, bool deleteAllRelatedData)
        {
            using (var session = DocumentStore.OpenSession())
            {
                try
                {
                    var q = from u in session.Query<User>().Customize(c => c.WaitForNonStaleResultsAsOfNow())
                            where u.Username == username && u.ApplicationName == ApplicationName
                            select u;
                    var user = q.SingleOrDefault();
                    if (user == null)
                    {
                        throw new NullReferenceException("The user could not be deleted.");
                    }

                    session.Delete(session.Load<ReservationForUniqueFieldValue>("username/" + user.Username));
                    session.Delete(session.Load<ReservationForUniqueFieldValue>("email/" + user.Email));

                    session.Delete(user);
                    session.SaveChanges();
                    return true;
                }
                catch (Exception ex)
                {
                    // TODO: log exception properly
                    Console.WriteLine(ex.ToString());
                    return false;
                }
            }
        }

        public override bool EnablePasswordReset
        {
            get { return true; }
        }

        public override bool EnablePasswordRetrieval
        {
            get { return false; }
        }

        public override MembershipUserCollection FindUsersByEmail(string emailToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            return FindUsers(u => u.Email.Contains(emailToMatch), pageIndex, pageSize, out totalRecords);
        }

        public override MembershipUserCollection FindUsersByName(string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            return FindUsers(u => u.Username.Contains(usernameToMatch), pageIndex, pageSize, out totalRecords);
        }

        public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
        {
            return FindUsers(null, pageIndex, pageSize, out totalRecords);
        }

        public override int GetNumberOfUsersOnline()
        {
            throw new NotImplementedException();
        }

        public override string GetPassword(string username, string answer)
        {
            throw new NotImplementedException();
        }

        public override MembershipUser GetUser(string username, bool userIsOnline)
        {
            using (var session = DocumentStore.OpenSession())
            {
                var q = from u in session.Query<User>().Customize(c => c.WaitForNonStaleResultsAsOfNow())
                        where u.Username == username && u.ApplicationName == ApplicationName
                        select u;
                var user = q.SingleOrDefault();
                if (user != null)
                {
                    return UserToMembershipUser(user);
                }
                return null;
            }
        }

        public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            using (var session = DocumentStore.OpenSession())
            {
                var user = session.Load<User>(providerUserKey.ToString());
                if (user != null)
                {
                    return UserToMembershipUser(user);
                }
                return null;
            }
        }

        public override string GetUserNameByEmail(string email)
        {
            using (var session = DocumentStore.OpenSession())
            {
                var q = from u in session.Query<User>()
                        where u.Email == email && u.ApplicationName == ApplicationName
                        select u.Username;
                return q.SingleOrDefault();
            }
        }

        public override int MaxInvalidPasswordAttempts
        {
            get { return 10; }
        }

        public override int MinRequiredNonAlphanumericCharacters
        {
            get { return 0; }
        }

        public override int MinRequiredPasswordLength { get { return minRequiredPasswordLength; } }

        public override int PasswordAttemptWindow
        {
            get { return 5; }
        }

        public override MembershipPasswordFormat PasswordFormat
        {
            get { return MembershipPasswordFormat.Hashed; }
        }

        public override string PasswordStrengthRegularExpression
        {
            get { return String.Empty; }
        }

        public override bool RequiresQuestionAndAnswer
        {
            get { return false; }
        }

        public override bool RequiresUniqueEmail
        {
            get { return false; }
        }

        public override string ResetPassword(string username, string answer)
        {
            using (var session = DocumentStore.OpenSession())
            {
                try
                {
                    var q = from u in session.Query<User>()
                            where u.Username == username && u.ApplicationName == ApplicationName
                            select u;
                    var user = q.SingleOrDefault();
                    if (user == null)
                    {
                        throw new Exception("The user to reset the password for could not be found.");
                    }
                    var newPassword = Membership.GeneratePassword(8, 2);
                    user.PasswordSalt = PasswordUtil.CreateRandomSalt();
                    user.PasswordHash = PasswordUtil.HashPassword(newPassword, user.PasswordSalt);

                    session.SaveChanges();
                    return newPassword;
                }
                catch (Exception ex)
                {
                    // TODO: log exception properly
                    Console.WriteLine(ex.ToString());
                    throw;
                }
            }
        }

        public override bool UnlockUser(string userName)
        {
            throw new NotImplementedException();
        }

        public override void UpdateUser(MembershipUser user)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            var username = user.UserName;
            SecUtility.CheckParameter(ref username, true, true, true, 0x100, "UserName");
            
            var email = user.Email;
            SecUtility.CheckParameter(ref email, RequiresUniqueEmail, RequiresUniqueEmail, false, 0x100, "Email");
            user.Email = email;

            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                try
                {
                    var q = from u in session.Query<User>()
                            where u.Username == user.UserName && u.ApplicationName == ApplicationName
                            select u;
                    var dbUser = q.SingleOrDefault();
                    if (dbUser == null)
                    {
                        throw new Exception("The user to update could not be found.");
                    }

                    var originalEmail = dbUser.Email;

                    if (originalEmail != user.Email)
                    {
                        session.Delete(session.Load<ReservationForUniqueFieldValue>("email/" + dbUser.Email));
                        session.Store(new ReservationForUniqueFieldValue { Id = "email/" + user.Email});
                    }

                    dbUser.Username = user.UserName;
                    dbUser.Email = user.Email;
                    dbUser.DateCreated = user.CreationDate;
                    dbUser.DateLastLogin = user.LastLoginDate;

                    session.SaveChanges();
                }
                catch(ConcurrencyException ex)
                {
                    var status = InterpretConcurrencyException(user.UserName, user.Email, ex);

                    if (status == MembershipCreateStatus.DuplicateEmail)
                        throw new ProviderException("The E-mail supplied is invalid.");
                    throw;
                }
            }
        }

        public override bool ValidateUser(string username, string password)
        {
            const bool updateLastLogin = true;

            return CheckPassword(username, password, updateLastLogin);
        }

        public override bool CheckPassword(string username, string password, bool updateLastLogin)
        {
            username = username.Trim();
            password = password.Trim();

            using (var session = DocumentStore.OpenSession())
            {
                var q = from u in session.Query<User>().Customize(c => c.WaitForNonStaleResultsAsOfNow())
                        where u.Username == username && u.ApplicationName == ApplicationName
                        select u;
                var user = q.SingleOrDefault();
                if (user != null && user.PasswordHash == PasswordUtil.HashPassword(password, user.PasswordSalt))
                {
                    if (updateLastLogin)
                    {
                        user.DateLastLogin = DateTime.Now;
                    }
                    session.SaveChanges();
                    return true;
                }
            }
            return false;
        }

        private MembershipUserCollection FindUsers(Func<User, bool> predicate, int pageIndex, int pageSize, out int totalRecords)
        {
            var membershipUsers = new MembershipUserCollection();
            using (var session = DocumentStore.OpenSession())
            {
                var q = from u in session.Query<User>()
                            where u.ApplicationName == ApplicationName
                        select u;
                var results = predicate != null ? q.Where(predicate) : q;
                var users = results as User[] ?? results.ToArray();
                totalRecords = users.Count();
                var pagedUsers = users.Skip(pageIndex * pageSize).Take(pageSize);
                foreach (var user in pagedUsers)
                {
                    membershipUsers.Add(UserToMembershipUser(user));
                }
            }
            return membershipUsers;
        }

        private MembershipUser UserToMembershipUser(User user)
        {
            return new RavenDbMembershipUser(providerName, user.Username, user.Id, user.Email, null, null, true, false
                , user.DateCreated, user.DateLastLogin.HasValue ? user.DateLastLogin.Value : new DateTime(1900, 1, 1), new DateTime(1900, 1, 1), new DateTime(1900, 1, 1), new DateTime(1900, 1, 1));
        }
    }
}
