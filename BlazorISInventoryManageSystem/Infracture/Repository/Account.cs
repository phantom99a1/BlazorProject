using Application.DTO.Request.Identity;
using Application.DTO.Response;
using Application.DTO.Response.Identity;
using Application.Extension.Identity;
using Application.Interface.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Infracstructure.Repository
{
    public class Account(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) : IAccount
    {
        public async Task<ServiceResponse> CreateUserAsync(CreateUserRequestDTO model)
        {
            var user = await FindUserByEmail(model.Email);
            if(user != null)
            {
                return new ServiceResponse(false, "User already exist!");
            }
            var newUser = new ApplicationUser
            {
                UserName = model.Email,
                PasswordHash = model.Password,
                Name = model.Name,
                Email = model.Email,
            };
            var result = CheckResult(await userManager.CreateAsync(newUser, model.Password));
            return !result.Flag ? result : await CreateUserClaims(model);
        }

        private async Task<ServiceResponse> CreateUserClaims(CreateUserRequestDTO model)
        {
            if (string.IsNullOrEmpty(model.Policy))
                return new ServiceResponse(false, "");
            Claim[] userClaims = [];
            if(model.Policy.Equals(Policy.AdminPolicy, StringComparison.OrdinalIgnoreCase))
            {
                userClaims = 
                [
                    new Claim(ClaimTypes.Email, model.Email),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim("Name", model.Name),
                    new Claim("Create", "true"),
                    new Claim("Read", "true"),
                    new Claim("Update", "true"),
                    new Claim("Delete", "true"),
                    new Claim("ManageUser", "true"),
                ];
            }
            else if(model.Policy.Equals(Policy.ManagePolicy, StringComparison.OrdinalIgnoreCase))
            {
                userClaims =
                [
                    new Claim(ClaimTypes.Email, model.Email),
                    new Claim(ClaimTypes.Role, "Manager"),
                    new Claim("Name", model.Name),
                    new Claim("Create", "true"),
                    new Claim("Read", "true"),
                    new Claim("Update", "true"),
                    new Claim("Delete", "false"),
                    new Claim("ManageUser", "false"),
                ];
            }
            else if(model.Policy.Equals(Policy.UserPolicy, StringComparison.OrdinalIgnoreCase))
            {
                userClaims =
                [
                    new Claim(ClaimTypes.Email, model.Email),
                    new Claim(ClaimTypes.Role, "User"),
                    new Claim("Name", model.Name),
                    new Claim("Create", "false"),
                    new Claim("Read", "false"),
                    new Claim("Update", "false"),
                    new Claim("Delete", "false"),
                    new Claim("ManageUser", "false"),
                ];
            }
            var result = CheckResult(await userManager.AddClaimsAsync(await FindUserByEmail(model.Email), userClaims));
            return !result.Flag ? result : new ServiceResponse(false, "User created");
        }

        private static ServiceResponse CheckResult(IdentityResult result)
        {
            if (result.Succeeded) return new ServiceResponse(true, null);
            var error = result.Errors.Select(m => m.Description);
            return new ServiceResponse(false, string.Join(Environment.NewLine, error));
        }

        public async Task<IEnumerable<GetUserWithClaimResponseDTO>> GetUsersWithClaimsAsync()
        {
            var UserList = new List<GetUserWithClaimResponseDTO>();
            var allUsers = await userManager.Users.ToListAsync();
            if (allUsers.Count == 0) return UserList;
            foreach (var user in allUsers)
            {
                var currentUser = await userManager.FindByIdAsync(user.Id);
                var getCurrentUserClaims = await userManager.GetClaimsAsync(currentUser ?? new ApplicationUser());
                if (getCurrentUserClaims.Any())
                {
                    UserList.Add(new GetUserWithClaimResponseDTO
                    {
                        UserID = user.Id,
                        Email = getCurrentUserClaims.FirstOrDefault(m => m.Type == ClaimTypes.Email)?.Value,
                        RoleName = getCurrentUserClaims.FirstOrDefault(m => m.Type == ClaimTypes.Role)?.Value,
                        Name = getCurrentUserClaims.FirstOrDefault(m => m.Type == "Name")?.Value,
                        ManageUser = Convert.ToBoolean(getCurrentUserClaims.FirstOrDefault(m => m.Type == "ManageUser")?.Value),
                        Create = Convert.ToBoolean(getCurrentUserClaims.FirstOrDefault(m => m.Type == "Create")?.Value),
                        Read = Convert.ToBoolean(getCurrentUserClaims.FirstOrDefault(m => m.Type == "Read")?.Value),
                        Update = Convert.ToBoolean(getCurrentUserClaims.FirstOrDefault(m => m.Type == "Update")?.Value),
                        Delete = Convert.ToBoolean(getCurrentUserClaims.FirstOrDefault(m => m.Type == "Delete")?.Value),
                    });
                }
            }
            return UserList;
        }

        public async Task SetUpAsync() => await CreateUserAsync(new CreateUserRequestDTO
        {
            Name = "Administrator",
            Email = "admin@admin.com",
            Password = "Admin123",
            Policy = Policy.AdminPolicy
        });

        public async Task<ServiceResponse> UpdateUserAsync(ChangeUserClaimRequestDTO model)
        {
            var user = await userManager.FindByIdAsync(model.UserID);
            if (user == null) return new ServiceResponse(false, "User not found");
            var oldUserClaims = await userManager.GetClaimsAsync(user);
            Claim[] newUserClaims = 
                [
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Role, model.RoleName),
                    new Claim(ClaimTypes.Name, model.Name),
                    new Claim("ManageUser", model.ManageUser.ToString()),
                    new Claim("Create", model.Create.ToString()),
                    new Claim("Read", model.Read.ToString()),
                    new Claim("Update", model.Update.ToString()),
                    new Claim("Delete", model.Delete.ToString()),
                ];
            var result = await userManager.RemoveClaimsAsync(user, oldUserClaims);
            var response = CheckResult(result);
            if (!response.Flag)
                return new ServiceResponse(false, response.Message);
            var addNewClaims = await userManager.AddClaimsAsync(user, newUserClaims);
            var outcome = CheckResult(addNewClaims);
            return outcome.Flag ? new ServiceResponse(true, "User updated") : outcome;
        } 

        public async Task<ServiceResponse> LoginAsync(LoginUserRequestDTO model)
        {
            var user = await FindUserByEmail(model.Email);
            if (user is null) return new ServiceResponse(false, "User not found");
            var verifyPassword = await signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!verifyPassword.Succeeded) return new ServiceResponse(false, "Incorrect credentials provided");
            var result = await signInManager.PasswordSignInAsync(user, model.Password, false, false);
            return result.Succeeded ? new ServiceResponse(true, null) : new ServiceResponse(false, "Unknown error occured while logging you in");
        }

        private async Task<ApplicationUser> FindUserByEmail(string email)
            => await userManager.FindByEmailAsync(email) ?? new ApplicationUser();

        private async Task<ApplicationUser> FindUserById(string id) => await userManager.FindByIdAsync(id) ?? new ApplicationUser();
    }
}
