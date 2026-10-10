using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity;

namespace LibraryManagementSystem.Data
{
    /// 
    /// Runs once at application startup (see Program.cs) to make sure the
    /// four roles the assignment requires exist, that a test login is
    /// available for each staff role, and that the catalogue has enough
    /// sample data to actually test borrowing/searching without first
    /// manually creating everything through the Admin CRUD pages.
    /// 
    public static class SeedData
    {
        /// 
        /// Entry point called from Program.cs on every startup. Every seeding
        /// step below is guarded by an existence check, so re-running this on
        /// an already-seeded database is safe and does not create duplicates.
        /// 
        public static async Task InitializeAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<LibraryDbContext>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Seed Identity Roles and Default Users
            string[] roles = { "Admin", "Reception", "Manager", "Member" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            await EnsureUserAsync(userManager, "admin@library.local", "Admin@12345", "Admin", "System Admin");
            await EnsureUserAsync(userManager, "reception@library.local", "Reception@12345", "Reception", "Front Desk");
            await EnsureUserAsync(userManager, "manager@library.local", "Manager@12345", "Manager", "Library Manager");

            // 2. Seed Books Sample Data
            if (!context.Books.Any())
            {
                context.Books.AddRange(
                    new Book { Name = "Clean Code", LibraryCode = "BK-1001", Description = "A handbook of agile software craftsmanship.", Author = "Robert C. Martin", Genre = "Technical", Status = "Available" },
                    new Book { Name = "The Pragmatic Programmer", LibraryCode = "BK-1002", Description = "Essential guide for software development best practices.", Author = "Andrew Hunt", Genre = "Technical", Status = "Available" },
                    new Book { Name = "Design Patterns", LibraryCode = "BK-1003", Description = "Elements of reusable object-oriented software.", Author = "Erich Gamma", Genre = "Technical", Status = "Available" },
                    new Book { Name = "To Kill a Mockingbird", LibraryCode = "BK-1004", Description = "A classic novel exploring justice and racial injustice.", Author = "Harper Lee", Genre = "Fiction", Status = "Available" },
                    new Book { Name = "Dune", LibraryCode = "BK-1005", Description = "Epic science fiction set on the desert planet Arrakis.", Author = "Frank Herbert", Genre = "Sci-Fi", Status = "Available" }
                );
            }

            // 3. Seed Music Sample Data
            if (!context.Music.Any())
            {
                context.Music.AddRange(
                    new Music { Name = "Abbey Road", LibraryCode = "MU-2001", Description = "Iconic studio album featuring classic rock tracks.", Artist = "The Beatles", ReleaseYear = 1969, Status = "Available" },
                    new Music { Name = "The Dark Side of the Moon", LibraryCode = "MU-2002", Description = "Legendary progressive rock album.", Artist = "Pink Floyd", ReleaseYear = 1973, Status = "Available" },
                    new Music { Name = "Thriller", LibraryCode = "MU-2003", Description = "Landmark pop album containing top chart hits.", Artist = "Michael Jackson", ReleaseYear = 1982, Status = "Available" },
                    new Music { Name = "Kind of Blue", LibraryCode = "MU-2004", Description = "Masterpiece of modal jazz recording.", Artist = "Miles Davis", ReleaseYear = 1959, Status = "Available" },
                    new Music { Name = "Rumours", LibraryCode = "MU-2005", Description = "Award-winning soft rock/pop album.", Artist = "Fleetwood Mac", ReleaseYear = 1977, Status = "Available" }
                );
            }

            // 4. Seed Toys Sample Data
            if (!context.Toys.Any())
            {
                context.Toys.AddRange(
                    new Toy { Name = "LEGO Classic Bricks Set", LibraryCode = "TY-3001", Description = "500-piece open-ended building brick kit.", Type = "Construction", MinimumAge = 4, Status = "Available" },
                    new Toy { Name = "Monopoly Classic", LibraryCode = "TY-3002", Description = "Standard fast-dealing property trading board game.", Type = "Board Game", MinimumAge = 8, Status = "Available" },
                    new Toy { Name = "Wooden Chess Set", LibraryCode = "TY-3003", Description = "Handcrafted wooden strategy game board with pieces.", Type = "Strategy Game", MinimumAge = 6, Status = "Available" },
                    new Toy { Name = "Rubik's Cube 3x3", LibraryCode = "TY-3004", Description = "Classic 3D combination puzzle challenge.", Type = "Puzzle", MinimumAge = 7, Status = "Available" },
                    new Toy { Name = "Magnetic Building Tiles", LibraryCode = "TY-3005", Description = "3D translucent magnetic tile building blocks.", Type = "Educational", MinimumAge = 3, Status = "Available" }
                );
            }

            // 5. Seed Borrowers Sample Data
            if (!context.Borrowers.Any())
            {
                context.Borrowers.AddRange(
                    new Borrower { FullName = "Alice Smith", Email = "alice.smith@example.com", Phone = "0412 345 678" },
                    new Borrower { FullName = "Bob Johnson", Email = "bob.johnson@example.com", Phone = "0423 456 789" },
                    new Borrower { FullName = "Charlie Brown", Email = "charlie.brown@example.com", Phone = "0434 567 890" },
                    new Borrower { FullName = "Diana Prince", Email = "diana.prince@example.com", Phone = "0445 678 901" },
                    new Borrower { FullName = "Evan Wright", Email = "evan.wright@example.com", Phone = "0456 789 012" }
                );
            }

            // Save sample data changes to database
            await context.SaveChangesAsync();
        }

        /// 
        /// Creates a login account with the given role if one doesn't already
        /// exist for that email. Used to seed the three staff test accounts
        /// (Admin/Reception/Manager) so there's something to log in with
        /// immediately after a fresh database is created.
        /// 
        private static async Task EnsureUserAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string role,
            string fullName)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing != null) return;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
