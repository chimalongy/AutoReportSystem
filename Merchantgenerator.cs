using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;


namespace ARS
{


        public class MerchantGenerator
        {
            // -------------------------------------------------------------------------
            // Seed Data
            // -------------------------------------------------------------------------

            private static readonly string[] FirstNames =
            {
            "Uche", "Emeka", "Chidi", "Ngozi", "Amaka", "Tunde", "Bola", "Segun",
            "Funmi", "Yemi", "Ade", "Kemi", "Wale", "Sola", "Tobi", "Femi",
            "Musa", "Ibrahim", "Aliyu", "Fatima", "Aisha", "Hauwa", "Yusuf", "Lawal",
            "Chukwu", "Obiora", "Ifeoma", "Chisom", "Ebuka", "Ifeanyi"
        };

            private static readonly string[] LastNames =
            {
            "Mohammed", "Okafor", "Adeleke", "Nwachukwu", "Balogun", "Abubakar",
            "Okonkwo", "Adeyemi", "Eze", "Obi", "Danjuma", "Garba", "Suleiman",
            "Bakare", "Fashola", "Tinubu", "Buhari", "Obaseki", "Wike", "Sanwo"
        };

            private static readonly string[] BusinessSuffixes =
            {
            "Stores", "Enterprises", "Trading Co.", "Global Ltd", "Solutions",
            "Ventures", "Associates", "Group", "Industries", "Services",
            "International", "Holdings", "Nigeria Ltd", "& Sons", "Supplies"
        };

            private static readonly string[] Streets =
            {
            "Ahmadu Bello Way", "Marina Street", "Broad Street", "Victoria Island",
            "Allen Avenue", "Awolowo Road", "Adeola Odeku Street", "Ozumba Mbadiwe",
            "Lekki-Epe Expressway", "Bonny Camp Road", "Balogun Street",
            "Nnamdi Azikiwe Street", "Herbert Macaulay Way", "Ikorodu Road",
            "Lagos-Ibadan Expressway", "Kano Road", "Murtala Mohammed Way",
            "Zaria Road", "Kaduna Bypass", "Port Harcourt Road"
        };

            private static readonly string[] Cities =
            {
            "Lagos", "Abuja", "Kano", "Ibadan", "Port Harcourt", "Benin City",
            "Kaduna", "Enugu", "Onitsha", "Aba", "Warri", "Ilorin", "Jos",
            "Maiduguri", "Zaria", "Abeokuta", "Asaba", "Uyo", "Calabar", "Owerri"
        };

            private static readonly string[] EmailDomains =
            {
            "gmail.com", "yahoo.com", "mail.com", "outlook.com",
            "hotmail.com", "live.com"
        };

            /// <summary>
            /// Nigerian phone prefixes — MTN, Airtel, Glo, 9mobile
            /// </summary>
            private static readonly string[] PhonePrefixes =
            {
            "0803", "0806", "0703", "0706", "0813", "0816", "0810", "0814",
            "0802", "0805", "0807", "0815", "0808", "0818", "0909", "0908"
        };

            // -------------------------------------------------------------------------
            // Private Fields
            // -------------------------------------------------------------------------

            private readonly Random _rng = new Random();

            // -------------------------------------------------------------------------
            // Data Generation Methods
            // -------------------------------------------------------------------------

            /// <summary>
            /// Returns a random full name (e.g. "Uche Mohammed").
            /// </summary>
            public string GenerateName()
            {
                string first = FirstNames[_rng.Next(FirstNames.Length)];
                string last = LastNames[_rng.Next(LastNames.Length)];
                return $"{first} {last}";
            }

            /// <summary>
            /// Returns a random business name (e.g. "Uche Mohammed Stores").
            /// </summary>
            public string GenerateBusinessName()
            {
                string name = GenerateName();
                string suffix = BusinessSuffixes[_rng.Next(BusinessSuffixes.Length)];
                return $"{name} {suffix}";
            }

            /// <summary>
            /// Returns a random Nigerian street address (e.g. "63 Ahmadu Bello Way, Kano").
            /// </summary>
            public string GenerateAddress()
            {
                int houseNo = _rng.Next(1, 250);
                string street = Streets[_rng.Next(Streets.Length)];
                string city = Cities[_rng.Next(Cities.Length)];
                return $"{houseNo} {street}, {city}";
            }

            /// <summary>
            /// Derives a plausible email address from a business name
            /// (e.g. "uche.mohammed1234@gmail.com").
            /// </summary>
            public string GenerateEmail(string businessName)
            {
                string slug = businessName
                    .ToLower()
                    .Replace(" ", ".")
                    .Replace("&", "and")
                    .Replace(",", "")
                    .Split('.')[0]; // keep only the first segment

                string domain = EmailDomains[_rng.Next(EmailDomains.Length)];
                int suffix = _rng.Next(1, 9999);
                return $"{slug}{suffix}@{domain}";
            }

            /// <summary>
            /// Returns a random Nigerian mobile number in E.164 format
            /// (e.g. "+2348037654321").
            /// </summary>
            public string GeneratePhone()
            {
                string prefix = PhonePrefixes[_rng.Next(PhonePrefixes.Length)];
                int number = _rng.Next(1_000_000, 9_999_999);
                // Convert local prefix 080x → +2348x
                return $"+234{prefix.Substring(1)}{number}";
            }

            // -------------------------------------------------------------------------
            // Bulk Insert Method
            // -------------------------------------------------------------------------

            /// <summary>
            /// Inserts <paramref name="totalRecords"/> merchants into
            /// <c>public.merchants</c> using PostgreSQL binary COPY for maximum
            /// throughput. Progress is written to <see cref="Console"/>.
            /// </summary>
            /// <param name="connectionString">Npgsql connection string.</param>
            /// <param name="totalRecords">Number of rows to insert (default 3 000 000).</param>
            /// <param name="batchSize">Rows per COPY batch (default 10 000).</param>
            public async Task InsertMerchantsAsync(
                string connectionString,
                int totalRecords = 3_000_000,
                int batchSize = 10_000)
            {
                int inserted = 0;

                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                Console.WriteLine($"Starting bulk insert of {totalRecords:N0} merchants...");
                var sw = Stopwatch.StartNew();

                while (inserted < totalRecords)
                {
                    int currentBatch = Math.Min(batchSize, totalRecords - inserted);

                    await using var writer = await conn.BeginBinaryImportAsync(
                        "COPY public.merchants " +
                        "  (merchant_name, merchant_address, merchant_email, merchant_phone) " +
                        "FROM STDIN (FORMAT BINARY)"
                    );

                    for (int i = 0; i < currentBatch; i++)
                    {
                        string businessName = GenerateBusinessName();

                        await writer.StartRowAsync();
                        await writer.WriteAsync(businessName, NpgsqlDbType.Varchar);
                        await writer.WriteAsync(GenerateAddress(), NpgsqlDbType.Varchar);
                        await writer.WriteAsync(GenerateEmail(businessName), NpgsqlDbType.Varchar);
                        await writer.WriteAsync(GeneratePhone(), NpgsqlDbType.Varchar);
                    }

                    await writer.CompleteAsync();
                    inserted += currentBatch;

                    if (inserted % 100_000 == 0 || inserted == totalRecords)
                    {
                        double pct = (double)inserted / totalRecords;
                        Console.WriteLine(
                            $"  [{sw.Elapsed:mm\\:ss}] {inserted:N0} / {totalRecords:N0} ({pct:P0})"
                        );
                    }
                }

                sw.Stop();
                Console.WriteLine(
                    $"\nDone. {totalRecords:N0} rows inserted in {sw.Elapsed:mm\\:ss\\.ff}"
                );
            }
        }
    
}
