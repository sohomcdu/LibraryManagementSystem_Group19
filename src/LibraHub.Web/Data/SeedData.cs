using LibraHub.Domain;
using LibraHub.Infrastructure;
using LibraHub.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Data;

/// <summary>
/// Demo data that mirrors spec section 6.4 / the mockups: Central, Northside and Riverside branches, Alex Morgan
/// (LC-2048-7731), Sam Lee at reception, Jordan Blake as manager, BK-100234 Clean Code, BK-100310 Dune, BK-100512 ...
/// All dates are relative to "today" so the screens always look current.
/// </summary>
public static class SeedData
{
    public const string DemoPassword = "Password123!";
    public const string DemoApiKey = "lh_demo_key_for_local_testing";

    record T(string Key, string Title, string? Sub, string Author, string Isbn, string Cat, string Pub, int Year, int Pages, string Desc);

    static readonly T[] Titles =
    {
        new("clean","Clean Code","A Handbook of Agile Software Craftsmanship","Robert C. Martin","9780132350884","Technology","Prentice Hall",2008,464,"Practical advice on writing readable, maintainable code."),
        new("ddia","Designing Data-Intensive Applications","The Big Ideas Behind Reliable, Scalable, and Maintainable Systems","Martin Kleppmann","9781449373320","Technology","O'Reilly Media",2017,616,"A practical guide to the principles behind modern data systems — storage engines, replication, partitioning, transactions and stream processing — and how to choose between them."),
        new("doet","The Design of Everyday Things",null,"Don Norman","9780465050659","Design","Basic Books",2013,368,"Why some products satisfy customers while others only frustrate them."),
        new("dp","Design Patterns","Elements of Reusable Object-Oriented Software","Gamma, Helm, Johnson, Vlissides","9780201633610","Technology","Addison-Wesley",1994,395,"The classic catalogue of object-oriented design patterns."),
        new("pragprog","The Pragmatic Programmer",null,"Andrew Hunt, David Thomas","9780135957059","Technology","Addison-Wesley",2019,352,"Timeless tips for software craftspeople."),
        new("refactoring","Refactoring",null,"Martin Fowler","9780134757599","Technology","Addison-Wesley",2018,448,"Improving the design of existing code."),
        new("cc","Code Complete",null,"Steve McConnell","9780735619678","Technology","Microsoft Press",2004,960,"A practical handbook of software construction."),
        new("coder","The Clean Coder",null,"Robert C. Martin","9780137081073","Technology","Prentice Hall",2011,256,"A code of conduct for professional programmers."),
        new("mmm","The Mythical Man-Month",null,"Frederick P. Brooks","9780201835953","Technology","Addison-Wesley",1995,336,"Essays on software engineering and project management."),
        new("dmmt","Don't Make Me Think",null,"Steve Krug","9780321965516","Design","New Riders",2014,216,"A common-sense approach to web usability."),
        new("dune","Dune",null,"Frank Herbert","9780441172719","Fiction","Ace",2005,608,"Politics, prophecy and spice on the desert planet Arrakis."),
        new("nw","Norwegian Wood",null,"Haruki Murakami","9780375704024","Fiction","Vintage",2000,296,"A nostalgic story of loss and sexuality in 1960s Tokyo."),
        new("1984","Nineteen Eighty-Four",null,"George Orwell","9780451524935","Fiction","Signet",1961,328,"A dystopian novel of surveillance and control."),
        new("pp","Pride and Prejudice",null,"Jane Austen","9780141439518","Fiction","Penguin Classics",2002,480,"Manners, marriage and misjudgement in Regency England."),
        new("sapiens","Sapiens","A Brief History of Humankind","Yuval Noah Harari","9780062316097","History","Harper",2015,464,"How Homo sapiens came to dominate the planet."),
        new("educated","Educated",null,"Tara Westover","9780399590504","Memoir","Random House",2018,352,"A memoir of growing up in a survivalist family and finding education."),
        new("mfs","Man's Search for Meaning",null,"Viktor E. Frankl","9780807014295","Memoir","Beacon Press",2006,184,"A psychiatrist's reflections on survival and purpose."),
        new("atomic","Atomic Habits",null,"James Clear","9780735211292","Self-Help","Avery",2018,320,"An easy and proven way to build good habits."),
        new("tfs","Thinking, Fast and Slow",null,"Daniel Kahneman","9780374533557","Science","Farrar, Straus and Giroux",2013,499,"The two systems that drive the way we think."),
        new("bhot","A Brief History of Time",null,"Stephen Hawking","9780553380163","Science","Bantam",1998,212,"From the Big Bang to black holes."),
        new("cosmos","Cosmos",null,"Carl Sagan","9780345539434","Science","Ballantine",2013,432,"A personal voyage through the universe."),
        new("gene","The Selfish Gene",null,"Richard Dawkins","9780198788607","Science","Oxford University Press",2016,496,"Evolution seen from the gene's point of view."),
        // Music & Toys
        new("abbey","Abbey Road",null,"The Beatles","CD-0001","Music","Apple Records",1969,0,"Classic Beatles album."),
        new("mozart","Mozart: Complete Symphonies",null,"Various Artists","CD-0002","Music","Deutsche Grammophon",2000,0,"Collected orchestral symphonies."),
        new("lego","LEGO Classic Bricks",null,"LEGO","TOY-0001","Toys","LEGO Group",2020,0,"Creative building set for children."),
        new("puzzle","Jigsaw Puzzle 1000pc",null,"Puzzle Co","TOY-0002","Toys","Puzzle Co",2018,0,"1000-piece scenic jigsaw puzzle."),
        // Additional Music
        new("rumours","Rumours",null,"Fleetwood Mac","CD-0003","Music","Warner Bros.",1977,0,"Grammy-winning rock album."),
        new("kindofblue","Kind of Blue",null,"Miles Davis","CD-0004","Music","Columbia",1959,0,"The best-selling jazz album of all time."),
        new("nevermind","Nevermind",null,"Nirvana","CD-0005","Music","DGC Records",1991,0,"Grunge classic featuring Smells Like Teen Spirit."),
        new("thriller","Thriller",null,"Michael Jackson","CD-0006","Music","Epic",1982,0,"The best-selling album of all time."),
        new("beethoven9","Symphony No. 9",null,"Ludwig van Beethoven","CD-0007","Music","Decca",1986,0,"Legendary classical symphony."),
        new("darkside","The Dark Side of the Moon",null,"Pink Floyd","CD-0008","Music","Harvest",1973,0,"Classic progressive rock album."),
        
        // Additional Toys
        new("monopoly","Monopoly Classic",null,"Hasbro","TOY-0005","Toys","Hasbro",1935,0,"Classic property trading board game."),
        new("scrabble","Scrabble",null,"Mattel","TOY-0006","Toys","Mattel",1938,0,"Classic word-building board game."),
        new("chess","Wooden Chess Set",null,"Classic Games","TOY-0007","Toys","Classic Games",2015,0,"Traditional wooden chess board and pieces."),
        new("uno","UNO Classic",null,"Mattel","TOY-0008","Toys","Mattel",1971,0,"The classic color and number matching card game."),
        new("magnatiles","Magna-Tiles 32-Piece",null,"Valtech","TOY-0009","Toys","Valtech",1997,0,"Magnetic building tiles for creative play."),
        new("rubiks","Rubik's Cube","3x3 puzzle","Ernő Rubik","TOY-0010","Toys","Ideal",1974,0,"Classic twisty puzzle.")
    };

    static readonly string[] Covers =
    {
        "#12324A,#0F766E","#B91C1C,#0F172A","#166534,#0F172A","#6D28D9,#1E1B4B","#B45309,#1F2937","#0E7490,#0F172A",
        "#9D174D,#1E1B4B","#3F6212,#0F172A","#1D4ED8,#0F172A","#7C2D12,#1C1917"
    };

    public static async Task RunAsync(AppDbContext db, IPasswordHasher<AppUser> hasher)
    {
        var now = Clock.Now; var today = Clock.Today;
        // If the database already has branches, only apply incremental seeds for newly added demo data
        if (await db.Branches.AnyAsync())
        {
            // ensure Music and Toys categories exist
            var existingCats = await db.Categories.AsNoTracking().Select(c => c.Name).ToListAsync();
            var need = new[] { "Music", "Toys" };
            foreach (var n in need) if (!existingCats.Contains(n)) db.Categories.Add(new Category { Name = n });
            await db.SaveChangesAsync();

            // ensure demo items (music/toys) exist at the expected branches
            var nthBranch = await db.Branches.FirstOrDefaultAsync(b => b.Code == "NTH");
            var rivBranch = await db.Branches.FirstOrDefaultAsync(b => b.Code == "RIV");
            var catsDict = (await db.Categories.AsNoTracking().ToListAsync()).ToDictionary(c => c.Name);
            var byKeyDict = Titles.ToDictionary(t => t.Key);

            Item AddSimple(string key, Branch at, ItemStatus st, string code, Branch? home = null)
            {
                var t = byKeyDict[key];
                var it = new Item
                {
                    Code = code,
                    Title = t.Title,
                    Subtitle = t.Sub,
                    Author = t.Author,
                    Isbn = t.Isbn,
                    CategoryId = catsDict[t.Cat].Id,
                    Publisher = t.Pub,
                    Year = t.Year,
                    Pages = t.Pages,
                    Description = t.Desc,
                    CoverColors = Covers[Array.FindIndex(Titles, x => x.Key == key) % Covers.Length],
                    HomeBranch = home ?? at,
                    CurrentBranch = at,
                    Status = st,
                    CreatedAt = now.AddDays(-30)
                };
                it.SearchText = ImportService.SearchTextFor(it, t.Cat);
                db.Items.Add(it); return it;
            }

            if (nthBranch != null)
            {
                if (!await db.Items.AnyAsync(i => i.Code == "MU-100001")) AddSimple("abbey", nthBranch, ItemStatus.Available, "MU-100001", nthBranch);
                if (!await db.Items.AnyAsync(i => i.Code == "TOY-0001")) AddSimple("lego", nthBranch, ItemStatus.Available, "TOY-0001", nthBranch);
                if (!await db.Items.AnyAsync(i => i.Code == "MU-100003"))
                {
                    var it3 = new Item
                    {
                        Code = "MU-100003",
                        Title = "The Dark Side of the Moon",
                        Subtitle = "",
                        Author = "Pink Floyd",
                        Isbn = "",
                        CategoryId = catsDict["Music"].Id,
                        Publisher = "Harvest",
                        Year = 1973,
                        Pages = 0,
                        Description = "Classic rock album",
                        CoverColors = Covers[0],
                        HomeBranch = nthBranch,
                        CurrentBranch = nthBranch,
                        Status = ItemStatus.Available,
                        CreatedAt = now.AddDays(-30)
                    };
                    it3.SearchText = ImportService.SearchTextFor(it3, "Music");
                    db.Items.Add(it3);
                }
                if (!await db.Items.AnyAsync(i => i.Code == "TOY-0003"))
                {
                    var t3 = new Item
                    {
                        Code = "TOY-0003",
                        Title = "Rubik's Cube",
                        Subtitle = "3x3 puzzle",
                        Author = "",
                        Isbn = "",
                        CategoryId = catsDict["Toys"].Id,
                        Publisher = "Ideal",
                        Year = 1974,
                        Pages = 0,
                        Description = "Classic twisty puzzle",
                        CoverColors = Covers[1],
                        HomeBranch = nthBranch,
                        CurrentBranch = nthBranch,
                        Status = ItemStatus.Available,
                        CreatedAt = now.AddDays(-30)
                    };
                    t3.SearchText = ImportService.SearchTextFor(t3, "Toys");
                    db.Items.Add(t3);
                }
            }
            if (rivBranch != null)
            {
                if (!await db.Items.AnyAsync(i => i.Code == "MU-100002")) AddSimple("mozart", rivBranch, ItemStatus.Available, "MU-100002", rivBranch);
                if (!await db.Items.AnyAsync(i => i.Code == "TOY-0002")) AddSimple("puzzle", rivBranch, ItemStatus.Available, "TOY-0002", rivBranch);
                if (!await db.Items.AnyAsync(i => i.Code == "MU-100004"))
                {
                    var it4 = new Item
                    {
                        Code = "MU-100004",
                        Title = "Abbey Road (Deluxe)",
                        Subtitle = "",
                        Author = "The Beatles",
                        Isbn = "",
                        CategoryId = catsDict["Music"].Id,
                        Publisher = "Apple Records",
                        Year = 1969,
                        Pages = 0,
                        Description = "Remastered classic album",
                        CoverColors = Covers[2],
                        HomeBranch = rivBranch,
                        CurrentBranch = rivBranch,
                        Status = ItemStatus.Available,
                        CreatedAt = now.AddDays(-30)
                    };
                    it4.SearchText = ImportService.SearchTextFor(it4, "Music");
                    db.Items.Add(it4);
                }
                if (!await db.Items.AnyAsync(i => i.Code == "TOY-0004"))
                {
                    var t4 = new Item
                    {
                        Code = "TOY-0004",
                        Title = "Wooden Train Set",
                        Subtitle = "Classic wooden toy",
                        Author = "",
                        Isbn = "",
                        CategoryId = catsDict["Toys"].Id,
                        Publisher = "ToyCo",
                        Year = 2010,
                        Pages = 0,
                        Description = "Durable wooden train set",
                        CoverColors = Covers[3],
                        HomeBranch = rivBranch,
                        CurrentBranch = rivBranch,
                        Status = ItemStatus.Available,
                        CreatedAt = now.AddDays(-30)
                    };
                    t4.SearchText = ImportService.SearchTextFor(t4, "Toys");
                    db.Items.Add(t4);
                }
            }
            await db.SaveChangesAsync();
            return;
        }

        // ---- settings, branches, desks, categories
        foreach (var kv in SettingsService.Defaults) db.Settings.Add(new Setting { Key = kv.Key, Value = kv.Value });
        var cen = new Branch { Code = "CEN", Name = "Central Library", OpensAt = new(9, 0), ClosesAt = new(20, 0), ClosedDays = "Sunday" };
        var nth = new Branch { Code = "NTH", Name = "Northside Branch", OpensAt = new(9, 0), ClosesAt = new(17, 0), ClosedDays = "Sunday" };
        var riv = new Branch { Code = "RIV", Name = "Riverside Branch", OpensAt = new(10, 0), ClosesAt = new(18, 0), ClosedDays = "Sunday,Monday" };
        db.Branches.AddRange(cen, nth, riv);
        var d1 = new Desk { Name = "Desk 1", Branch = cen }; var d2 = new Desk { Name = "Desk 2", Branch = cen };
        var d3 = new Desk { Name = "Desk 3", Branch = cen }; var dn = new Desk { Name = "Desk 1", Branch = nth };
        var dr = new Desk { Name = "Desk 1", Branch = riv };
        var kc = new Desk { Name = "Kiosk 1", Branch = cen, IsKiosk = true };
        var kn = new Desk { Name = "Kiosk 1", Branch = nth, IsKiosk = true };
        var kr = new Desk { Name = "Kiosk 1", Branch = riv, IsKiosk = true };
        db.Desks.AddRange(d1, d2, d3, dn, dr, kc, kn, kr);
        var cats = new[] { "Technology", "Design", "Fiction", "Science", "History", "Memoir", "Self-Help", "Music", "Toys", "Uncategorised" }
            .ToDictionary(n => n, n => new Category { Name = n });
        db.Categories.AddRange(cats.Values);

        // ---- users
        AppUser U(string email, string name, Role role, Branch? home = null, Desk? desk = null, string? card = null,
            string? pin = null, string? mobile = null, Channels? overdue = null)
        {
            var u = new AppUser { Email = email, FullName = name, Role = role, HomeBranch = home, Desk = desk, CardNumber = card, Mobile = mobile,
                CreatedAt = now.AddMonths(-7) };
            u.PasswordHash = hasher.HashPassword(u, DemoPassword);
            if (pin != null) u.PinHash = hasher.HashPassword(u, pin);
            if (overdue != null) u.PrefOverdue = overdue.Value;
            db.Users.Add(u); return u;
        }
        var alex = U("alex.morgan@example.com", "Alex Morgan", Role.Patron, cen, null, "LC-2048-7731", "4321", "0400 000 000", Channels.Sms);
        alex.CreatedAt = new DateTime(today.Year, 2, 3);
        var nguyen = U("j.nguyen@example.com", "J. Nguyen", Role.Patron, cen, null, "LC-3101-1001", "1111", "0400 000 001");
        var sharma = U("p.sharma@example.com", "P. Sharma", Role.Patron, cen, null, "LC-3101-1002", "2222", null);
        var chen = U("l.chen@example.com", "L. Chen", Role.Patron, nth, null, "LC-3101-1003", "3333", "0400 000 003");
        var patel = U("r.patel@example.com", "R. Patel", Role.Patron, cen, null, "LC-3101-1004", "4444", "0400 000 004");
        var rossi = U("m.rossi@example.com", "M. Rossi", Role.Patron, riv, null, "LC-3101-1005", "5555", "0400 000 005");
        var baker = U("t.baker@example.com", "T. Baker", Role.Patron, nth, null, "LC-3101-1006", "6666", null);
        var sam = U("sam.lee@librahub.local", "Sam Lee", Role.Reception, cen, d1);
        U("nina.osei@librahub.local", "Nina Osei", Role.Reception, nth, dn);
        U("jordan.blake@librahub.local", "Jordan Blake", Role.Manager, cen, d1);
        U("admin@librahub.local", "Library IT Admin", Role.Admin, cen, d1);
        U("kiosk.central@librahub.local", "Kiosk · Central", Role.Kiosk, cen, kc);
        U("kiosk.northside@librahub.local", "Kiosk · Northside", Role.Kiosk, nth, kn);
        U("kiosk.riverside@librahub.local", "Kiosk · Riverside", Role.Kiosk, riv, kr);
        var patrons = new[] { alex, nguyen, sharma, chen, patel, rossi, baker };

        // ---- items
        var byKey = Titles.ToDictionary(t => t.Key);
        var items = new List<(string Key, Item Item)>();
        int auto = 100800;
        Item Add(string key, Branch at, ItemStatus st, string? code = null, string? note = null, Branch? home = null)
        {
            var t = byKey[key];
            var it = new Item
            {
                Code = code ?? $"BK-{auto++:D6}", Title = t.Title, Subtitle = t.Sub, Author = t.Author, Isbn = t.Isbn,
                Category = cats[t.Cat], Publisher = t.Pub, Year = t.Year, Pages = t.Pages, Description = t.Desc,
                CoverColors = Covers[Array.FindIndex(Titles, x => x.Key == key) % Covers.Length],
                HomeBranch = home ?? at, CurrentBranch = at, Status = st, StatusNote = note, CreatedAt = now.AddDays(-400)
            };
            it.SearchText = ImportService.SearchTextFor(it, t.Cat);
            db.Items.Add(it); items.Add((key, it)); return it;
        }

        var cleanA = Add("clean", cen, ItemStatus.Available, "BK-100234");
        Add("clean", cen, ItemStatus.Available);
        Add("clean", cen, ItemStatus.Borrowed);
        Add("clean", nth, ItemStatus.Borrowed);
        Add("ddia", cen, ItemStatus.Borrowed); Add("ddia", cen, ItemStatus.Borrowed);
        Add("ddia", nth, ItemStatus.InTransit, note: "Moving to Central");
        Add("ddia", riv, ItemStatus.Damaged, note: $"In repair · est. back {Fmt.Day(today.AddDays(7))}");
        var doetC = Add("doet", cen, ItemStatus.Available, "BK-100512"); Add("doet", nth, ItemStatus.Available); Add("doet", riv, ItemStatus.Available);
        var dpItem = Add("dp", riv, ItemStatus.InTransit, home: cen);
        Add("pragprog", cen, ItemStatus.Available); Add("pragprog", cen, ItemStatus.Available); Add("pragprog", nth, ItemStatus.Available);
        var refNth = Add("refactoring", nth, ItemStatus.Available);
        var refCen = Add("refactoring", cen, ItemStatus.Reserved);
        var ccItem = Add("cc", cen, ItemStatus.Borrowed);
        Add("coder", cen, ItemStatus.Available); Add("coder", riv, ItemStatus.Available);
        Add("mmm", cen, ItemStatus.Available); Add("mmm", nth, ItemStatus.Available);
        Add("dmmt", cen, ItemStatus.Available); Add("dmmt", riv, ItemStatus.Available);
        var duneRes = Add("dune", cen, ItemStatus.Reserved, "BK-100310");
        var duneOut = Add("dune", cen, ItemStatus.Borrowed, "BK-100311");
        var duneNth = Add("dune", nth, ItemStatus.Available, "BK-100312", home: cen);
        var nwItem = Add("nw", nth, ItemStatus.Reserved, "BK-100777");
        Add("1984", cen, ItemStatus.Available); Add("1984", nth, ItemStatus.Available); Add("1984", riv, ItemStatus.Available);
        Add("pp", cen, ItemStatus.Available); Add("pp", riv, ItemStatus.Available);
        var sapNth = Add("sapiens", nth, ItemStatus.Borrowed);
        var sapRiv = Add("sapiens", riv, ItemStatus.Available, home: cen);
        var eduCen = Add("educated", cen, ItemStatus.Borrowed); Add("educated", riv, ItemStatus.Available);
        Add("mfs", cen, ItemStatus.Available); Add("mfs", nth, ItemStatus.Available);
        var atomicOut = Add("atomic", cen, ItemStatus.Borrowed);
        Add("atomic", cen, ItemStatus.Available); Add("atomic", cen, ItemStatus.Available); Add("atomic", nth, ItemStatus.Available);
        Add("tfs", cen, ItemStatus.Available); Add("tfs", nth, ItemStatus.Available);
        Add("bhot", nth, ItemStatus.Available); Add("bhot", riv, ItemStatus.Available);
        Add("cosmos", riv, ItemStatus.Available); Add("cosmos", cen, ItemStatus.Available);
        Add("gene", cen, ItemStatus.Available); Add("gene", nth, ItemStatus.Available);
        // Music & Toys items at other branches
        Add("abbey", nth, ItemStatus.Available, "MU-100001", home: nth);
        Add("mozart", riv, ItemStatus.Available, "MU-100002", home: riv);
        Add("lego", nth, ItemStatus.Available, "TOY-100001", home: nth);
        Add("puzzle", riv, ItemStatus.Available, "TOY-100002", home: riv);
        await db.SaveChangesAsync();

        // ---- current loans (Alex: Educated due today, Sapiens 7 days overdue, Atomic Habits 12 days left)
        Loan AddLoan(Item i, AppUser p, DateTime borrowed, DateTime due, Desk desk, DateTime? returned = null)
        {
            var l = new Loan { Item = i, Patron = p, Desk = desk, BorrowedAt = borrowed, DueAt = due, ReturnedAt = returned };
            db.Loans.Add(l); return l;
        }
        AddLoan(eduCen, alex, today.AddDays(-14).AddHours(10), today, d1);
        var sapLoan = AddLoan(sapNth, alex, today.AddDays(-21).AddHours(11), today.AddDays(-7), dn);
        AddLoan(atomicOut, alex, today.AddDays(-2).AddHours(15), today.AddDays(12), d1);
        var ccLoan = AddLoan(ccItem, nguyen, today.AddDays(-18).AddHours(9), today.AddDays(-4), d2);
        var duneLoan = AddLoan(duneOut, sharma, today.AddDays(-16).AddHours(14), today.AddDays(-2), d1);
        AddLoan(items.First(x => x.Key == "clean" && x.Item.Status == ItemStatus.Borrowed && x.Item.CurrentBranch == cen).Item, rossi, today.AddDays(-5).AddHours(13), today.AddDays(9), d3);
        AddLoan(items.First(x => x.Key == "clean" && x.Item.CurrentBranch == nth).Item, baker, today.AddDays(-3).AddHours(16), today.AddDays(11), dn);
        var ddiaOut = items.Where(x => x.Key == "ddia" && x.Item.Status == ItemStatus.Borrowed).Select(x => x.Item).ToList();
        AddLoan(ddiaOut[0], patel, today.AddDays(-8).AddHours(10), today.AddDays(6), d1);
        AddLoan(ddiaOut[1], rossi, today.AddDays(-12).AddHours(12), today.AddDays(2), d2);

        // ---- history for reports: ~600 returned loans over the last ~5 months (deterministic)
        var rnd = new Random(42);
        var allDesks = new[] { d1, d2, d3, dn, dr, kc, kn, kr };
        var lateLoans = new List<Loan>();
        for (int n = 0; n < 620; n++)
        {
            var it = items[rnd.Next(items.Count)].Item;
            var back = rnd.Next(3, 150);
            var recentBias = rnd.NextDouble() < 0.5 ? rnd.Next(3, 60) : back;      // gentle upward trend
            var borrowed = today.AddDays(-recentBias).AddHours(9 + rnd.Next(8));
            var days = rnd.NextDouble() < 0.91 ? rnd.Next(4, 15) : rnd.Next(15, 22);
            var returned = borrowed.AddDays(days);
            if (returned > now) returned = now.AddHours(-1);
            var desk = allDesks.Where(d => d.Branch == it.CurrentBranch).OrderBy(_ => rnd.Next()).First();
            var l = AddLoan(it, patrons[rnd.Next(patrons.Length)], borrowed, borrowed.Date.AddDays(14), desk, returned);
            if (returned.Date > l.DueAt.Date) lateLoans.Add(l);
        }
        // a few loans borrowed and returned today so the dashboard KPIs are not empty
        for (int n = 0; n < 14; n++)
        {
            var it = items[rnd.Next(items.Count)].Item;
            var desk = allDesks.Where(d => d.Branch == it.CurrentBranch && !d.IsKiosk).First();
            AddLoan(it, patrons[rnd.Next(patrons.Length)], today.AddHours(8 + rnd.Next(3)), today.AddDays(14), desk, Clock.Now.AddMinutes(-5));
        }
        await db.SaveChangesAsync();

        // ---- fines: overdue (accruing) + settled history
        db.Fines.Add(new Fine { Loan = sapLoan, Patron = alex, DaysLate = 7, AmountCents = 350, IssuedAt = now, Desk = dn });
        db.Fines.Add(new Fine { Loan = ccLoan, Patron = nguyen, DaysLate = 4, AmountCents = 200, IssuedAt = now, Desk = d2 });
        db.Fines.Add(new Fine { Loan = duneLoan, Patron = sharma, DaysLate = 2, AmountCents = 100, IssuedAt = now, Desk = d1 });
        int f = 0;
        foreach (var l in lateLoans)
        {
            var late = (l.ReturnedAt!.Value.Date - l.DueAt.Date).Days;
            var st = f % 9 == 0 ? FineStatus.Waived : f % 7 == 0 ? FineStatus.Outstanding : FineStatus.Collected;
            db.Fines.Add(new Fine { Loan = l, Patron = l.Patron, Desk = l.Desk, DaysLate = late, AmountCents = late * 50,
                IssuedAt = l.ReturnedAt.Value, Status = st, SettledAt = st == FineStatus.Outstanding ? null : l.ReturnedAt.Value.AddHours(1) });
            f++;
        }

        // ---- holds / waitlists (F5)
        Reservation R(string key, AppUser p, Branch pick, DateTime placed, HoldStatus st, Item? ready = null, int? expDays = null)
        {
            var t = byKey[key];
            var r = new Reservation { Isbn = t.Isbn, TitleText = t.Title, Patron = p, PickupBranch = pick, PlacedAt = placed, QueueKey = placed, Status = st };
            if (ready != null)
            {
                r.ReadyItem = ready; r.ReadyAt = now.AddDays(-1); r.ExpiresAt = today.AddDays(expDays ?? 3); r.PickupCode = "LH" + Random.Shared.Next(1000, 9999);
            }
            db.Reservations.Add(r); return r;
        }
        R("dune", alex, cen, now.AddDays(-9), HoldStatus.Ready, duneRes, 2);
        R("nw", chen, nth, now.AddDays(-8), HoldStatus.Ready, nwItem, 1);
        R("refactoring", patel, cen, now.AddDays(-10), HoldStatus.Ready, refCen, 3);
        R("nw", alex, cen, now.AddDays(-2), HoldStatus.Waiting);
        R("cc", alex, cen, now.AddDays(-1), HoldStatus.Waiting);
        R("ddia", nguyen, cen, now.AddDays(-20), HoldStatus.Waiting);
        R("ddia", sharma, cen, now.AddDays(-14), HoldStatus.Waiting);
        R("ddia", chen, nth, now.AddDays(-9), HoldStatus.Waiting);
        R("ddia", patel, riv, now.AddDays(-4), HoldStatus.Waiting);

        // ---- transfers (F4)
        BranchTransfer Tr(Item i, Branch from, Branch to, int daysAgo, TransferStatus st, string reason, string by)
        {
            var t = new BranchTransfer { Item = i, FromBranch = from, ToBranch = to, Status = st, Reason = reason,
                RequestedAt = now.AddDays(-daysAgo), RequestedBy = by };
            if (st is TransferStatus.Dispatched or TransferStatus.Received) { t.DispatchedAt = t.RequestedAt.AddHours(3); t.DispatchedBy = by; }
            if (st == TransferStatus.Received) { t.ReceivedAt = t.RequestedAt.AddDays(2); t.ReceivedBy = "Nina Osei"; }
            db.Transfers.Add(t); return t;
        }
        Tr(sapRiv, cen, riv, 12, TransferStatus.Received, "Patron request", "Sam Lee");
        Tr(duneNth, cen, nth, 10, TransferStatus.Received, "Rebalancing", "Sam Lee");
        Tr(dpItem, riv, cen, 6, TransferStatus.Dispatched, "Patron request", "Sam Lee");
        Tr(refNth, nth, cen, 4, TransferStatus.Requested, "Patron request", "Nina Osei");
        var ddiaTransit = items.First(x => x.Key == "ddia" && x.Item.Status == ItemStatus.InTransit).Item;
        Tr(ddiaTransit, nth, cen, 3, TransferStatus.Dispatched, "Patron request", "Nina Osei");

        // ---- history, notifications, API key
        foreach (var (_, it) in items)
            db.ItemEvents.Add(new ItemEvent { Item = it, At = it.CreatedAt, Text = "Added to catalogue", By = "import" });
        db.Notifications.Add(new NotificationLog { Patron = alex, Recipient = alex.Mobile!, Type = NotificationType.Overdue, Channel = Channel.Sms,
            Subject = "—", Body = "Hi Alex, “Sapiens” is 7 days overdue. Current fine: $3.50. Please return it.", Status = SendStatus.SimulatedSent, CreatedAt = today.AddHours(8) });
        db.Notifications.Add(new NotificationLog { Patron = alex, Recipient = alex.Email, Type = NotificationType.HoldAvailable, Channel = Channel.Email,
            Subject = "Your hold is ready", Body = $"Hi Alex, “Dune” is ready at Central Library. Collect by {Fmt.Day(today.AddDays(2))}.", Status = SendStatus.SimulatedSent, CreatedAt = today.AddDays(-1).AddHours(14) });
        db.Notifications.Add(new NotificationLog { Patron = null, Recipient = "Staff · Central Library", Type = NotificationType.Transfer, Channel = Channel.Email,
            Subject = "Transfer received", Body = "Sapiens arrived at Riverside Branch from Central Library.", Status = SendStatus.SimulatedSent, CreatedAt = now.AddHours(-3) });
        db.ApiKeys.Add(new ApiKey { Name = "Demo community app", KeyHash = ApiKeyService.Hash(DemoApiKey), Prefix = DemoApiKey[..8], CreatedAt = now.AddDays(-30) });
        db.ImportJobs.Add(new ImportJob { Source = "CSV · initial-load.csv", UserName = "Library IT Admin", At = now.AddDays(-30), TotalRows = 53, Imported = 51, Skipped = 0, Errors = 2 });
        await db.SaveChangesAsync();
    }
}
