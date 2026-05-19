using System.Text.RegularExpressions;
using HtmlAgilityPack;

var baseUrl = "https://auspost.com.au";
var web = new HtmlWeb();

// Path to the downloaded CSV (fixed path)
var csvPath = "PostcodeCrawler/australian-postcodes.csv";
var postcodeCoords = new Dictionary<string, (string Lat, string Lon)>();

if (File.Exists(csvPath))
{
    var lines = File.ReadAllLines(csvPath);
    foreach (var line in lines.Skip(1)) // Skip header
    {
        var parts = line.Split(',');
        if (parts.Length >= 5)
        {
            var postcode = parts[0].Trim('"');
            var lat = parts[3].Trim('"');
            var lon = parts[4].Trim('"');
            if (!postcodeCoords.ContainsKey(postcode))
            {
                postcodeCoords[postcode] = (lat, lon);
            }
        }
    }
}
else
{
    Console.WriteLine($"CSV file not found at {csvPath}");
}

var allResults = new List<SuburbInfo>();
var outputCsvPath = "suburbs_output.csv";

// Iterate through letters a to z
for (char c = 'a'; c <= 'z'; c++)
{
    var letterUrl = $"{baseUrl}/postcode/suburb-index/{c}";
    Console.WriteLine($"\n--- Processing letter: {c.ToString().ToUpper()} ---");
    
    var indexPages = new HashSet<string> { letterUrl };
    
    // Load the base letter page to find additional pages (a2, a3, etc.)
    var letterDoc = web.Load(letterUrl);
    var paginationNodes = letterDoc.DocumentNode.SelectNodes($"//a[contains(@href, '/postcode/suburb-index/{c}')]");
    if (paginationNodes != null)
    {
        foreach (var pNode in paginationNodes)
        {
            var pLink = pNode.GetAttributeValue("href", "");
            if (!string.IsNullOrEmpty(pLink))
            {
                var fullPLink = pLink.StartsWith("http") ? pLink : baseUrl + pLink;
                indexPages.Add(fullPLink);
            }
        }
    }

    foreach (var indexUrl in indexPages.OrderBy(x => x))
    {
        Console.WriteLine($"Scraping index page: {indexUrl}");
        var doc = web.Load(indexUrl);
        var suburbNodes = doc.DocumentNode.SelectNodes("//a[contains(@href, '/postcode/')]");

        if (suburbNodes != null)
        {
            foreach (var node in suburbNodes)
            {
                var href = node.GetAttributeValue("href", "");
                var suburbName = node.InnerText.Trim();
                
                // Skip index links and invalid names
                if (href.Contains("/postcode/suburb-index/")) continue;
                if (string.IsNullOrWhiteSpace(suburbName) || suburbName.Length <= 1) continue;

                // Fetch the postcode page
                var suburbDoc = web.Load(baseUrl + href);
                
                var postcodeNode = suburbDoc.DocumentNode.SelectSingleNode("//table//a[string-length(text())=4 and number(text()) > 0]");
                if (postcodeNode == null)
                {
                     postcodeNode = suburbDoc.DocumentNode.SelectSingleNode("//a[string-length(text())=4 and starts-with(@href, '/postcode/')]");
                }

                string postcode = postcodeNode?.InnerText.Trim() ?? "N/A";
                string lat = "N/A";
                string lon = "N/A";

                if (postcode != "N/A" && postcodeCoords.TryGetValue(postcode, out var coords))
                {
                    lat = coords.Lat;
                    lon = coords.Lon;
                }

                allResults.Add(new SuburbInfo { Suburb = suburbName, Postcode = postcode, Lat = lat, Long = lon });
                Console.WriteLine($"Extracted: {suburbName}, Postcode: {postcode}, Lat: {lat}, Long: {lon}");
                
                Thread.Sleep(200); // Be polite to the server
                // if (allResults.Count >= 20) goto Finalize; 
            }
        }
    }
}

// Finalize:
if (allResults.Count > 0)
{
    Console.WriteLine($"\nFinal Results Summary ({allResults.Count} suburbs):");
    
    // Write to CSV
    using (var writer = new StreamWriter(outputCsvPath))
    {
        writer.WriteLine("Suburb,Postcode,Latitude,Longitude");
        foreach (var res in allResults)
        {
            var escapedSuburb = res.Suburb.Contains(",") ? $"\"{res.Suburb}\"" : res.Suburb;
            writer.WriteLine($"{escapedSuburb},{res.Postcode},{res.Lat},{res.Long}");
        }
    }
    Console.WriteLine($"\nResults saved to {Path.GetFullPath(outputCsvPath)}");
}
else
{
    Console.WriteLine("No suburbs found.");
}

public class SuburbInfo
{
    public string Suburb { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string Lat { get; set; } = "";
    public string Long { get; set; } = "";
}