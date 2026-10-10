using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
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
            string[] roles = { "Admin", "Reception", "Manager", "Member", "Kiosk" };
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
            await EnsureUserAsync(userManager, "member@library.local", "Member@12345", "Member", "Demo Member");
            await EnsureUserAsync(userManager, "kiosk.central@librahub.local", "Password123!", "Kiosk", "Central Kiosk Device");
            await EnsureUserAsync(userManager, "kiosk.northside@librahub.local", "Password123!", "Kiosk", "Northside Kiosk Device");
            await EnsureUserAsync(userManager, "kiosk.riverside@librahub.local", "Password123!", "Kiosk", "Riverside Kiosk Device");

            var demoMember = await userManager.FindByEmailAsync("member@library.local");
            if (demoMember != null)
            {
                var demoBorrower = await context.Borrowers
                    .FirstOrDefaultAsync(b => b.ApplicationUserId == demoMember.Id || b.Email == demoMember.Email);
                if (demoBorrower == null)
                {
                    context.Borrowers.Add(new Borrower
                    {
                        FullName = demoMember.FullName,
                        Email = demoMember.Email!,
                        Phone = string.Empty,
                        ApplicationUserId = demoMember.Id
                    });
                    await context.SaveChangesAsync();
                }
                else if (demoBorrower.ApplicationUserId == null)
                {
                    demoBorrower.ApplicationUserId = demoMember.Id;
                    await context.SaveChangesAsync();
                }
            }

            // 2. Seed branches and reception desks before cataloguing items.
            var branchSeeds = new[]
            {
                new Branch { Code = "CEN", Name = "Central Library", Address = "1 Civic Square, Darwin", IsActive = true },
                new Branch { Code = "NTH", Name = "Northside Branch", Address = "25 Casuarina Road, Darwin", IsActive = true },
                new Branch { Code = "RIV", Name = "Riverside Branch", Address = "8 River Street, Darwin", IsActive = true }
            };
            foreach (var branch in branchSeeds)
                if (!context.Branches.Any(b => b.Code == branch.Code)) context.Branches.Add(branch);
            await context.SaveChangesAsync();
            var centralId = context.Branches.Where(b => b.Code == "CEN").Select(b => b.Id).First();
            var northId = context.Branches.Where(b => b.Code == "NTH").Select(b => b.Id).First();
            var riverId = context.Branches.Where(b => b.Code == "RIV").Select(b => b.Id).First();
            if (!context.Desks.Any())
            {
                context.Desks.AddRange(
                    new Desk { Label = "Central Desk 1", BranchId = centralId, IsActive = true },
                    new Desk { Label = "Northside Desk 1", BranchId = northId, IsActive = true },
                    new Desk { Label = "Riverside Desk 1", BranchId = riverId, IsActive = true }
                );
                await context.SaveChangesAsync();
            }

            // 3. Seed Books Sample Data
            if (!context.Books.Any())
            {
                context.Books.AddRange(
                    new Book { Name = "Clean Code", LibraryCode = "BK-1001", Description = "A handbook of agile software craftsmanship.", Author = "Robert C. Martin", Genre = "Technical", Status = "Available", BranchId = centralId },
                    new Book { Name = "The Pragmatic Programmer", LibraryCode = "BK-1002", Description = "Essential guide for software development best practices.", Author = "Andrew Hunt", Genre = "Technical", Status = "Available", BranchId = centralId },
                    new Book { Name = "Design Patterns", LibraryCode = "BK-1003", Description = "Elements of reusable object-oriented software.", Author = "Erich Gamma", Genre = "Technical", Status = "Available", BranchId = centralId },
                    new Book { Name = "To Kill a Mockingbird", LibraryCode = "BK-1004", Description = "A classic novel exploring justice and racial injustice.", Author = "Harper Lee", Genre = "Fiction", Status = "Available", BranchId = centralId },
                    new Book { Name = "Dune", LibraryCode = "BK-1005", Description = "Epic science fiction set on the desert planet Arrakis.", Author = "Frank Herbert", Genre = "Sci-Fi", Status = "Available", BranchId = centralId }
                );
            }

            // 3. Seed Music Sample Data
            if (!context.Music.Any())
            {
                context.Music.AddRange(
                    new Music { Name = "Abbey Road", LibraryCode = "MU-2001", Description = "Iconic studio album featuring classic rock tracks.", Artist = "The Beatles", ReleaseYear = 1969, Status = "Available", BranchId = centralId },
                    new Music { Name = "The Dark Side of the Moon", LibraryCode = "MU-2002", Description = "Legendary progressive rock album.", Artist = "Pink Floyd", ReleaseYear = 1973, Status = "Available", BranchId = centralId },
                    new Music { Name = "Thriller", LibraryCode = "MU-2003", Description = "Landmark pop album containing top chart hits.", Artist = "Michael Jackson", ReleaseYear = 1982, Status = "Available", BranchId = centralId },
                    new Music { Name = "Kind of Blue", LibraryCode = "MU-2004", Description = "Masterpiece of modal jazz recording.", Artist = "Miles Davis", ReleaseYear = 1959, Status = "Available", BranchId = centralId },
                    new Music { Name = "Rumours", LibraryCode = "MU-2005", Description = "Award-winning soft rock/pop album.", Artist = "Fleetwood Mac", ReleaseYear = 1977, Status = "Available", BranchId = centralId }
                );
            }

            // 4. Seed Toys Sample Data
            if (!context.Toys.Any())
            {
                context.Toys.AddRange(
                    new Toy { Name = "LEGO Classic Bricks Set", LibraryCode = "TY-3001", Description = "500-piece open-ended building brick kit.", Type = "Construction", MinimumAge = 4, Status = "Available", BranchId = centralId },
                    new Toy { Name = "Monopoly Classic", LibraryCode = "TY-3002", Description = "Standard fast-dealing property trading board game.", Type = "Board Game", MinimumAge = 8, Status = "Available", BranchId = centralId },
                    new Toy { Name = "Wooden Chess Set", LibraryCode = "TY-3003", Description = "Handcrafted wooden strategy game board with pieces.", Type = "Strategy Game", MinimumAge = 6, Status = "Available", BranchId = centralId },
                    new Toy { Name = "Rubik's Cube 3x3", LibraryCode = "TY-3004", Description = "Classic 3D combination puzzle challenge.", Type = "Puzzle", MinimumAge = 7, Status = "Available", BranchId = centralId },
                    new Toy { Name = "Magnetic Building Tiles", LibraryCode = "TY-3005", Description = "3D translucent magnetic tile building blocks.", Type = "Educational", MinimumAge = 3, Status = "Available", BranchId = centralId }
                );
            }

            // Add more music and toy titles on every startup using stable library codes.
            // Existence checks make this safe to run repeatedly on existing databases.
            var extraMusic = new[]
            {
                new Music { Name = "Blue Train", LibraryCode = "MU-2006", Description = "Influential hard-bop jazz album.", Artist = "John Coltrane", ReleaseYear = 1957, Status = "Available", BranchId = centralId },
                new Music { Name = "Back to Black", LibraryCode = "MU-2007", Description = "Soul and pop album released in 2006.", Artist = "Amy Winehouse", ReleaseYear = 2006, Status = "Available", BranchId = northId },
                new Music { Name = "Discovery", LibraryCode = "MU-2008", Description = "Electronic music album with a futuristic sound.", Artist = "Daft Punk", ReleaseYear = 2001, Status = "Available", BranchId = riverId },
                new Music { Name = "A Love Supreme", LibraryCode = "MU-2009", Description = "A celebrated spiritual jazz recording.", Artist = "John Coltrane", ReleaseYear = 1965, Status = "Available", BranchId = centralId },
                new Music { Name = "The Planets", LibraryCode = "MU-2010", Description = "Orchestral suite by Gustav Holst.", Artist = "Various Performers", ReleaseYear = 1916, Status = "Available", BranchId = northId }
            };
            foreach (var item in extraMusic)
                if (!context.Items.Any(i => i.LibraryCode == item.LibraryCode)) context.Music.Add(item);

            var extraToys = new[]
            {
                new Toy { Name = "Jigsaw Puzzle: World Map", LibraryCode = "TY-3006", Description = "A family-friendly 500-piece world map puzzle.", Type = "Puzzle", MinimumAge = 8, Status = "Available", BranchId = centralId },
                new Toy { Name = "Cooperative Board Game", LibraryCode = "TY-3007", Description = "A cooperative strategy game for small groups.", Type = "Board Game", MinimumAge = 7, Status = "Available", BranchId = northId },
                new Toy { Name = "Beginner Science Kit", LibraryCode = "TY-3008", Description = "Simple supervised experiments for young learners.", Type = "Educational", MinimumAge = 8, Status = "Available", BranchId = riverId },
                new Toy { Name = "Wooden Train Set", LibraryCode = "TY-3009", Description = "A durable wooden railway play set.", Type = "Construction", MinimumAge = 3, Status = "Available", BranchId = centralId },
                new Toy { Name = "Tangram Shape Puzzle", LibraryCode = "TY-3010", Description = "Classic geometric shape and problem-solving puzzle.", Type = "Puzzle", MinimumAge = 6, Status = "Available", BranchId = northId },
                new Toy { Name = "Memory Matching Cards", LibraryCode = "TY-3011", Description = "Picture-matching cards to build memory skills.", Type = "Educational", MinimumAge = 3, Status = "Available", BranchId = riverId }
            };
            foreach (var item in extraToys)
                if (!context.Items.Any(i => i.LibraryCode == item.LibraryCode)) context.Toys.Add(item);

            // Assign existing legacy catalogue items to Central if they have no branch.
            foreach (var item in context.Items.Where(i => i.BranchId == null)) item.BranchId = centralId;

            // 6. Seed Borrowers Sample Data
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
        /// exist for that email. Seeds demo staff and member accounts so
        /// there's something to log in with after a fresh database is created.
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
