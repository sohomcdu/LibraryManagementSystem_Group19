# Sample data sources

These CSV files are the app's sample catalogue: 50 books, 50 albums and 50 toys. They were taken from the public datasets below and then trimmed and cleaned to fit the app.

## References (APA 7th)

- Internet Archive. (n.d.). *Open Library Covers API*. Open Library. Retrieved October 14, 2026, from https://openlibrary.org/dev/docs/api/covers
- R for Data Science Online Learning Community. (2019). *Board games database* (TidyTuesday, 12 March 2019) [Data set]. GitHub. https://github.com/rfordatascience/tidytuesday/tree/main/data/2019/2019-03-12
- R for Data Science Online Learning Community. (2022). *LEGO sets* (TidyTuesday, 6 September 2022) [Data set]. GitHub. https://github.com/rfordatascience/tidytuesday/tree/main/data/2022/2022-09-06
- R for Data Science Online Learning Community. (2024). *Rolling Stone album rankings* (TidyTuesday, 7 May 2024) [Data set]. GitHub. https://github.com/rfordatascience/tidytuesday/tree/main/data/2024/2024-05-07
- Zając, Z. (2017). *goodbooks-10k: A new dataset for book recommendations* [Data set]. GitHub. https://github.com/zygmuntz/goodbooks-10k

Original data owners: Goodreads (books), Rolling Stone and The Pudding (albums), BoardGameGeek (board games), Rebrickable (LEGO sets). Book cover images are loaded live from Open Library using each book's ISBN.

## Files

| File | Rows | Taken from |
|---|---|---|
| `books.csv` | 50 books | goodbooks-10k: 50 popular English-language titles chosen for a mix of genres (classics, children's, fantasy, sci-fi, mystery, biography, non-fiction) |
| `music.csv` | 50 albums | Rolling Stone 500 Greatest Albums, 2020 ranking: ranks 57 to 108 |
| `toys.csv` | 35 board games + 15 LEGO sets | BoardGameGeek: the most-rated games after the first 35. Rebrickable: LEGO sets from 2019-2022 |
| `books_with_errors.csv` | 5 rows | Hand-made with deliberate mistakes, to demonstrate the importer's validation |

## What was changed
- **Book codes** follow the app's rule `BK-` plus a number in the 1000s: **BK-1205 to BK-1254**. These numbers come after BK-1001 to BK-1005 (the seed data) and BK-1006 to BK-1204 (an earlier sample set), so they never clash with either. Music and toy codes are generated when the files are loaded (MU-2xxx and TY-3xxx).
- **Book descriptions** are short original summaries written for this project. They are not taken from the dataset or from publishers. Music and toy descriptions are built from the dataset fields: ranking and genre, players and playing time, set number and piece count.
- **Genres** were assigned by hand to match the app's genre list.
- **ISBN-10s** are converted to ISBN-13.
- **Long subtitles** are shortened (e.g. "Outliers: The Story of Success" becomes "Outliers"), and only the first author is kept. Accents are removed (e.g. Les Miserables, Gabriel Garcia Marquez). No value contains a comma.
- Items are spread evenly across the CEN, NTH and RIV branches, with one copy each.
- Titles already in the app's seed data were left out.
- LEGO minimum ages are estimated from piece count, because the dataset does not include age ratings.

## How the data gets into the app
- **Automatically:** when the app starts, `Services/SampleDataSeeder.cs` loads `books.csv`, `music.csv` and `toys.csv` from this folder. Anything already in the database is skipped, so restarting never creates duplicates.
- **Manually:** the same files can be uploaded at **Administration → CSV Importer**, which shows the validation and duplicate handling.
- `books_with_errors.csv` is never loaded automatically. Upload it in the CSV Importer to see the validation reject it and save nothing.
