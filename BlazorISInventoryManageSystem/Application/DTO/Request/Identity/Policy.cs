namespace Application.DTO.Request.Identity
{
    public static class Policy
    {
        public const string AdminPolicy = "AdminPolicy";
        public const string ManagePolicy = "ManagePolicy";
        public const string UserPolicy = "UserPolicy";

        public static class RoleClaim
        {
            public const string Admin = "Admin";
            public const string Manage = "Manage";
            public const string User = "User";
        }
    }
}
