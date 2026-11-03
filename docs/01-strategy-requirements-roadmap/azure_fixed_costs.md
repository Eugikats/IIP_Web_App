# Azure fixed costs per dedicated client environment

Prepared 2026-10-12. All figures are USD, pay-as-you-go retail list price, per month, per client environment. Source: the public Azure Retail Prices API (`https://prices.azure.com/api/retail/prices`, `type eq Consumption`, every lookup resolved to exactly one meter by script). Monthly = hourly x 730 or daily x 30.42. Line costs are rounded to cents before summing, so totals equal the sum of the displayed lines.

Files: this report, `azure_fixed_costs.csv` (every line, every region and tier, add-ons included) and `azure_margin_check.csv` (margin inputs and result per region, tier and scenario).

## 1. Findings

1. **In South Africa North the fixed cost is 40% to 45% above the current estimates** (Core $88.98 vs $63.73, Growth $164.26 vs $113.30, Enterprise $215.51 vs $149.93).
2. **All three tiers fall below the 80% margin line in South Africa North**: 75.0%, 75.0% and 75.5% on the 10 requested lines, and 69.2%, 72.1% and 73.4% once Defender for SQL and Storage are added.
3. **The gap is compute and database, not storage or logging.** App Service plus SQL is 88% of the Core total. For those two lines South Africa North costs 41% to 43% more than West Europe; every other line differs by a few dollars at most. The current estimates sit close to East US and West Europe prices (East US Core is $62.30), not South Africa North prices.
4. West Europe and East US land on the line: Core and Growth clear 80% by 0.2 to 1.1 points, and Enterprise clears it only in East US (80.26%), missing in West Europe (79.76%). The current estimates themselves clear 80% by only about half a point (section 3c).
5. **Negotiated rates: none apply.** `az` is logged in to subscription `5f5765ae-985f-42e4-a443-8e2447be33ca` (display name "Microsoft Azure Sponsorship", quota `PayAsYouGo_2014-09-01`). Its billing account `f46f138f-1b78-458f-baa1-5471eeee31f2` is type `MicrosoftOnlineServicesProgram`, an individual pay-as-you-go account, which is billed at list price. `az consumption pricesheet show` and the REST price sheet (api-versions 2019-10-01, 2021-10-01, 2023-05-01) all return 404 "Cost management data is not supported for subscription", and the billing-account scope returns 400 "entity type BillingAccount is not supported". There is no negotiated price sheet to compare against, so the retail figures in this report are the rates this subscription is charged.

## 2. Totals per tier against the current estimates

Base = the 10 requested lines. Defender and AI Search are optional add-ons, excluded from the base total and shown separately.

| Region | Tier | Computed fixed cost | Current estimate | Difference $ | Difference % |
|---|---|---:|---:|---:|---:|
| South Africa North (primary) | Core | 88.98 | 63.73 | +25.25 | +39.6% |
| South Africa North (primary) | Growth | 164.26 | 113.30 | +50.96 | +45.0% |
| South Africa North (primary) | Enterprise | 215.51 | 149.93 | +65.58 | +43.7% |
| West Europe | Core | 65.11 | 63.73 | +1.38 | +2.2% |
| West Europe | Growth | 119.03 | 113.30 | +5.73 | +5.1% |
| West Europe | Enterprise | 159.83 | 149.93 | +9.90 | +6.6% |
| East US | Core | 62.30 | 63.73 | -1.43 | -2.2% |
| East US | Growth | 114.88 | 113.30 | +1.58 | +1.4% |
| East US | Enterprise | 153.23 | 149.93 | +3.30 | +2.2% |

Optional add-on totals (per environment, per month):

| Region | Tier | Base | Base + Defender SQL + Defender Storage + malware scanning | AI Search Basic (1 unit, on top) | AI Search S1 (1 unit, on top) |
|---|---|---:|---:|---:|---:|
| South Africa North (primary) | Core | 88.98 | 114.51 | +73.73 | +245.28 |
| South Africa North (primary) | Growth | 164.26 | 190.54 | +73.73 | +245.28 |
| South Africa North (primary) | Enterprise | 215.51 | 244.04 | +73.73 | +245.28 |
| West Europe | Core | 65.11 | 90.64 | +73.73 | +245.28 |
| West Europe | Growth | 119.03 | 145.31 | +73.73 | +245.28 |
| West Europe | Enterprise | 159.83 | 188.36 | +73.73 | +245.28 |
| East US | Core | 62.30 | 87.83 | +73.73 | +245.28 |
| East US | Growth | 114.88 | 141.16 | +73.73 | +245.28 |
| East US | Enterprise | 153.23 | 181.76 | +73.73 | +245.28 |

## 3. Margin check (worst case, paid annually, UGX 4,100 = $1)

Revenue a month: Core $447.15, Growth $894.31, Enterprise $1,341.46. Cost = fixed + people (Core $8.00, Growth $26.67, Enterprise $66.00) + bank/FX (0.5% of revenue + 1.5% of fixed) + allowance (2,000 / 4,500 / 6,500 credits x $0.005 x 1.125 = $11.25 / $25.31 / $36.56). Margin = (revenue - cost) / revenue. A tier is flagged when margin is below 80%.

### 3a. South Africa North, base 10 lines (the worst case)

| Tier | Revenue | Fixed | People | Bank/FX | Allowance | Total cost | Margin $ | Margin % | Flag |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Core | 447.15 | 88.98 | 8.00 | 3.57 | 11.25 | 111.80 | 335.35 | 75.00% | BELOW 80% |
| Growth | 894.31 | 164.26 | 26.67 | 6.94 | 25.31 | 223.18 | 671.13 | 75.04% | BELOW 80% |
| Enterprise | 1,341.46 | 215.51 | 66.00 | 9.94 | 36.56 | 328.01 | 1,013.45 | 75.55% | BELOW 80% |

### 3b. South Africa North, base plus Defender for SQL, Defender for Storage and malware scanning

| Tier | Fixed | Total cost | Margin $ | Margin % | Flag |
|---|---:|---:|---:|---:|---|
| Core | 114.51 | 137.71 | 309.44 | 69.20% | BELOW 80% |
| Growth | 190.54 | 249.85 | 644.46 | 72.06% | BELOW 80% |
| Enterprise | 244.04 | 356.97 | 984.49 | 73.39% | BELOW 80% |

### 3c. All regions and scenarios

| Region | Tier | Margin % base | Flag | Margin % with Defender | Flag |
|---|---|---:|---|---:|---|
| South Africa North (primary) | Core | 75.00% | BELOW 80% | 69.20% | BELOW 80% |
| South Africa North (primary) | Growth | 75.04% | BELOW 80% | 72.06% | BELOW 80% |
| South Africa North (primary) | Enterprise | 75.55% | BELOW 80% | 73.39% | BELOW 80% |
| West Europe | Core | 80.42% | ok | 74.62% | BELOW 80% |
| West Europe | Growth | 80.18% | ok | 77.20% | BELOW 80% |
| West Europe | Enterprise | 79.76% | BELOW 80% | 77.60% | BELOW 80% |
| East US | Core | 81.05% | ok | 75.26% | BELOW 80% |
| East US | Growth | 80.65% | ok | 77.67% | BELOW 80% |
| East US | Enterprise | 80.26% | ok | 78.10% | BELOW 80% |

Reference: the margin implied by the current estimates is Core 80.73%, Growth 80.83%, Enterprise 80.51%. They clear 80% by only about half a point, so a small price movement breaks them.

### 3d. What it would take to reach 80% in South Africa North (base lines)

| Tier | Highest fixed cost that still gives 80% | Computed fixed cost | Over by | Revenue needed at computed cost | Price rise needed |
|---|---:|---:|---:|---:|---:|
| Core | 66.94 | 88.98 | 22.04 | 561.87 | 25.7% |
| Growth | 120.60 | 164.26 | 43.66 | 1,121.57 | 25.4% |
| Enterprise | 156.67 | 215.51 | 58.84 | 1,647.72 | 22.8% |

Storage and logging levers do not close this. Moving SQL long-term backup from RA-GRS to LRS and also crediting the free first 5 GB of Log Analytics ingestion moves the South Africa North margin only slightly (Core 75.0% to 76.4%, Growth 75.0% to 76.4%, Enterprise 75.5% to 76.8%). The free 5 GB is per billing account, not per environment, so it cannot be counted on for every client.

## 4. Line items per region

Each tier cell shows quantity and unit, then the monthly cost in USD. `retailPrice` is the API value for the stated unitOfMeasure.

### South Africa North (primary)

| Line | skuName | meterName | unitOfMeasure | retailPrice (USD) | Core: qty, $/mo | Growth: qty, $/mo | Enterprise: qty, $/mo |
|---|---|---|---|---:|---|---|---|
| App Service plan, Linux B2 | B2 | B2 | 1 Hour | 0.0487 | 1 instances, 35.55 | 1 instances, 35.55 | 2 instances, 71.10 |
| Azure SQL Database Standard S1 | S1 | S1 DTUs | 1/Day | 1.413 | 1 database, 42.98 | - | - |
| Azure SQL Database Standard S2 | S2 | S2 DTUs | 1/Day | 3.533 | - | 1 database, 107.47 | 1 database, 107.47 |
| Blob Hot LRS: data stored | Hot LRS | Hot LRS Data Stored | 1 GB/Month | 0.0219 | 50 GB, 1.09 | 100 GB, 2.19 | 250 GB, 5.47 |
| Blob Hot LRS: read ops (50% of ops) | Hot LRS | Hot Read Operations | 10K | 0.004 | 50,000 ops, 0.02 | 100,000 ops, 0.04 | 250,000 ops, 0.10 |
| Blob Hot LRS: write ops (50% of ops) | Hot LRS | Hot LRS Write Operations | 10K | 0.06 | 50,000 ops, 0.30 | 100,000 ops, 0.60 | 250,000 ops, 1.50 |
| Log Analytics ingestion | Analytics Logs | Analytics Logs Data Ingestion | 1 GB | 3.36 | 1.3 GB, 4.37 | 2.6 GB, 8.74 | 3.5 GB, 11.76 |
| SQL LTR backup storage (RA-GRS) | Backup RA-GRS | Backup RA-GRS Data Stored | 1 GB/Month | 0.067 | 50 GB, 3.35 | 100 GB, 6.70 | 150 GB, 10.05 |
| SQL auditing: stored logs (estimate) | Hot LRS | Hot LRS Data Stored | 1 GB/Month | 0.0219 | 5 GB, 0.11 | 10 GB, 0.22 | 15 GB, 0.33 |
| SQL auditing: write ops (estimate) | Hot LRS | Hot LRS Write Operations | 10K | 0.06 | 50,000 ops, 0.30 | 100,000 ops, 0.60 | 150,000 ops, 0.90 |
| ACS Email: sent | Basic | Basic Sent Email | 1 | 0.00025 | 1,000 emails, 0.25 | 5,000 emails, 1.25 | 20,000 emails, 5.00 |
| ACS Email: data transferred | Basic | Basic Data Transferred | 1 MB | 0.00012 | 500 MB, 0.06 | 2,500 MB, 0.30 | 10,000 MB, 1.20 |
| Azure DNS public zone | Public | Public Zone | 1 | 0.5 | 1 zone, 0.50 | 1 zone, 0.50 | 1 zone, 0.50 |
| Key Vault operations | Standard | Operations | 10K | 0.03 | 10,000 ops, 0.03 | 10,000 ops, 0.03 | 20,000 ops, 0.06 |
| Service Bus Basic | Basic | Basic Messaging Operations | 1M | 0.073 | 1,000,000 ops, 0.07 | 1,000,000 ops, 0.07 | 1,000,000 ops, 0.07 |
| **Total (10 requested lines)** | | | | | **88.98** | **164.26** | **215.51** |

Optional add-ons, South Africa North (primary):

| Line | skuName | meterName | unitOfMeasure | retailPrice (USD) | Core: qty, $/mo | Growth: qty, $/mo | Enterprise: qty, $/mo |
|---|---|---|---|---:|---|---|---|
| Defender for Azure SQL (per server) | Standard | Standard Node | 1/Month | 15 | 1 server, 15.00 | 1 server, 15.00 | 1 server, 15.00 |
| Defender for Storage (per account) | Standard | Standard Node | 1/Hour | 0.0134 | 1 account, 9.78 | 1 account, 9.78 | 1 account, 9.78 |
| Defender for Storage malware scanning | Malware Scanning | Malware Scanning Data Ingested | 1 GB | 0.15 | 5 GB, 0.75 | 10 GB, 1.50 | 25 GB, 3.75 |

| Line | skuName | meterName | unitOfMeasure | retailPrice (USD) | Core: qty, $/mo | Growth: qty, $/mo | Enterprise: qty, $/mo |
|---|---|---|---|---:|---|---|---|
| AI Search Basic (1 unit) | Basic | Basic Unit | 1 Hour | 0.101 | 1 unit, 73.73 | 1 unit, 73.73 | 1 unit, 73.73 |
| AI Search S1 (1 unit) | Standard S1 | Standard S1 Unit | 1 Hour | 0.336 | 1 unit, 245.28 | 1 unit, 245.28 | 1 unit, 245.28 |

### West Europe

| Line | skuName | meterName | unitOfMeasure | retailPrice (USD) | Core: qty, $/mo | Growth: qty, $/mo | Enterprise: qty, $/mo |
|---|---|---|---|---:|---|---|---|
| App Service plan, Linux B2 | B2 | B2 | 1 Hour | 0.036 | 1 instances, 26.28 | 1 instances, 26.28 | 2 instances, 52.56 |
| Azure SQL Database Standard S1 | S1 | S1 DTUs | 1/Day | 0.9677 | 1 database, 29.44 | - | - |
| Azure SQL Database Standard S2 | S2 | S2 DTUs | 1/Day | 2.42 | - | 1 database, 73.62 | 1 database, 73.62 |
| Blob Hot LRS: data stored | Hot LRS | Hot LRS Data Stored | 1 GB/Month | 0.0196 | 50 GB, 0.98 | 100 GB, 1.96 | 250 GB, 4.90 |
| Blob Hot LRS: read ops (50% of ops) | Hot LRS | Hot Read Operations | 10K | 0.0043 | 50,000 ops, 0.02 | 100,000 ops, 0.04 | 250,000 ops, 0.11 |
| Blob Hot LRS: write ops (50% of ops) | Hot LRS | Hot LRS Write Operations | 10K | 0.054 | 50,000 ops, 0.27 | 100,000 ops, 0.54 | 250,000 ops, 1.35 |
| Log Analytics ingestion | Analytics Logs | Analytics Logs Data Ingestion | 1 GB | 2.99 | 1.3 GB, 3.89 | 2.6 GB, 7.77 | 3.5 GB, 10.46 |
| SQL LTR backup storage (RA-GRS) | Backup RA-GRS | Backup RA-GRS Data Stored | 1 GB/Month | 0.0595 | 50 GB, 2.97 | 100 GB, 5.95 | 150 GB, 8.92 |
| SQL auditing: stored logs (estimate) | Hot LRS | Hot LRS Data Stored | 1 GB/Month | 0.0196 | 5 GB, 0.10 | 10 GB, 0.20 | 15 GB, 0.29 |
| SQL auditing: write ops (estimate) | Hot LRS | Hot LRS Write Operations | 10K | 0.054 | 50,000 ops, 0.27 | 100,000 ops, 0.54 | 150,000 ops, 0.81 |
| ACS Email: sent | Basic | Basic Sent Email | 1 | 0.00025 | 1,000 emails, 0.25 | 5,000 emails, 1.25 | 20,000 emails, 5.00 |
| ACS Email: data transferred | Basic | Basic Data Transferred | 1 MB | 0.00012 | 500 MB, 0.06 | 2,500 MB, 0.30 | 10,000 MB, 1.20 |
| Azure DNS public zone | Public | Public Zone | 1 | 0.5 | 1 zone, 0.50 | 1 zone, 0.50 | 1 zone, 0.50 |
| Key Vault operations | Standard | Operations | 10K | 0.03 | 10,000 ops, 0.03 | 10,000 ops, 0.03 | 20,000 ops, 0.06 |
| Service Bus Basic | Basic | Basic Messaging Operations | 1M | 0.05 | 1,000,000 ops, 0.05 | 1,000,000 ops, 0.05 | 1,000,000 ops, 0.05 |
| **Total (10 requested lines)** | | | | | **65.11** | **119.03** | **159.83** |

Optional add-ons, West Europe:

| Line | skuName | meterName | unitOfMeasure | retailPrice (USD) | Core: qty, $/mo | Growth: qty, $/mo | Enterprise: qty, $/mo |
|---|---|---|---|---:|---|---|---|
| Defender for Azure SQL (per server) | Standard | Standard Node | 1/Month | 15 | 1 server, 15.00 | 1 server, 15.00 | 1 server, 15.00 |
| Defender for Storage (per account) | Standard | Standard Node | 1/Hour | 0.0134 | 1 account, 9.78 | 1 account, 9.78 | 1 account, 9.78 |
| Defender for Storage malware scanning | Malware Scanning | Malware Scanning Data Ingested | 1 GB | 0.15 | 5 GB, 0.75 | 10 GB, 1.50 | 25 GB, 3.75 |

| Line | skuName | meterName | unitOfMeasure | retailPrice (USD) | Core: qty, $/mo | Growth: qty, $/mo | Enterprise: qty, $/mo |
|---|---|---|---|---:|---|---|---|
| AI Search Basic (1 unit) | Basic | Basic Unit | 1 Hour | 0.101 | 1 unit, 73.73 | 1 unit, 73.73 | 1 unit, 73.73 |
| AI Search S1 (1 unit) | Standard S1 | Standard S1 Unit | 1 Hour | 0.336 | 1 unit, 245.28 | 1 unit, 245.28 | 1 unit, 245.28 |

### East US

| Line | skuName | meterName | unitOfMeasure | retailPrice (USD) | Core: qty, $/mo | Growth: qty, $/mo | Enterprise: qty, $/mo |
|---|---|---|---|---:|---|---|---|
| App Service plan, Linux B2 | B2 | B2 | 1 Hour | 0.034 | 1 instances, 24.82 | 1 instances, 24.82 | 2 instances, 49.64 |
| Azure SQL Database Standard S1 | S1 | S1 DTUs | 1/Day | 0.9677 | 1 database, 29.44 | - | - |
| Azure SQL Database Standard S2 | S2 | S2 DTUs | 1/Day | 2.42 | - | 1 database, 73.62 | 1 database, 73.62 |
| Blob Hot LRS: data stored | Hot LRS | Hot LRS Data Stored | 1 GB/Month | 0.0208 | 50 GB, 1.04 | 100 GB, 2.08 | 250 GB, 5.20 |
| Blob Hot LRS: read ops (50% of ops) | Hot LRS | Hot Read Operations | 10K | 0.004 | 50,000 ops, 0.02 | 100,000 ops, 0.04 | 250,000 ops, 0.10 |
| Blob Hot LRS: write ops (50% of ops) | Hot LRS | Hot LRS Write Operations | 10K | 0.05 | 50,000 ops, 0.25 | 100,000 ops, 0.50 | 250,000 ops, 1.25 |
| Log Analytics ingestion | Analytics Logs | Analytics Logs Data Ingestion | 1 GB | 2.3 | 1.3 GB, 2.99 | 2.6 GB, 5.98 | 3.5 GB, 8.05 |
| SQL LTR backup storage (RA-GRS) | Backup RA-GRS | Backup RA-GRS Data Stored | 1 GB/Month | 0.05 | 50 GB, 2.50 | 100 GB, 5.00 | 150 GB, 7.50 |
| SQL auditing: stored logs (estimate) | Hot LRS | Hot LRS Data Stored | 1 GB/Month | 0.0208 | 5 GB, 0.10 | 10 GB, 0.21 | 15 GB, 0.31 |
| SQL auditing: write ops (estimate) | Hot LRS | Hot LRS Write Operations | 10K | 0.05 | 50,000 ops, 0.25 | 100,000 ops, 0.50 | 150,000 ops, 0.75 |
| ACS Email: sent | Basic | Basic Sent Email | 1 | 0.00025 | 1,000 emails, 0.25 | 5,000 emails, 1.25 | 20,000 emails, 5.00 |
| ACS Email: data transferred | Basic | Basic Data Transferred | 1 MB | 0.00012 | 500 MB, 0.06 | 2,500 MB, 0.30 | 10,000 MB, 1.20 |
| Azure DNS public zone | Public | Public Zone | 1 | 0.5 | 1 zone, 0.50 | 1 zone, 0.50 | 1 zone, 0.50 |
| Key Vault operations | Standard | Operations | 10K | 0.03 | 10,000 ops, 0.03 | 10,000 ops, 0.03 | 20,000 ops, 0.06 |
| Service Bus Basic | Basic | Basic Messaging Operations | 1M | 0.05 | 1,000,000 ops, 0.05 | 1,000,000 ops, 0.05 | 1,000,000 ops, 0.05 |
| **Total (10 requested lines)** | | | | | **62.30** | **114.88** | **153.23** |

Optional add-ons, East US:

| Line | skuName | meterName | unitOfMeasure | retailPrice (USD) | Core: qty, $/mo | Growth: qty, $/mo | Enterprise: qty, $/mo |
|---|---|---|---|---:|---|---|---|
| Defender for Azure SQL (per server) | Standard | Standard Node | 1/Month | 15 | 1 server, 15.00 | 1 server, 15.00 | 1 server, 15.00 |
| Defender for Storage (per account) | Standard | Standard Node | 1/Hour | 0.0134 | 1 account, 9.78 | 1 account, 9.78 | 1 account, 9.78 |
| Defender for Storage malware scanning | Malware Scanning | Malware Scanning Data Ingested | 1 GB | 0.15 | 5 GB, 0.75 | 10 GB, 1.50 | 25 GB, 3.75 |

| Line | skuName | meterName | unitOfMeasure | retailPrice (USD) | Core: qty, $/mo | Growth: qty, $/mo | Enterprise: qty, $/mo |
|---|---|---|---|---:|---|---|---|
| AI Search Basic (1 unit) | Basic | Basic Unit | 1 Hour | 0.101 | 1 unit, 73.73 | 1 unit, 73.73 | 1 unit, 73.73 |
| AI Search S1 (1 unit) | Standard S1 | Standard S1 Unit | 1 Hour | 0.336 | 1 unit, 245.28 | 1 unit, 245.28 | 1 unit, 245.28 |

## 5. Assumptions, exclusions and caveats

Quantities you gave are used as given. The items below were not specified and are my assumptions; change them and rerun if they are wrong.

- **Blob operations:** split 50% read, 50% write. Writes cost 12 to 15 times reads, so an all-write pattern would add up to about $0.28 (Core) to $1.40 (Enterprise) a month.
- **Blob Hot LRS storage:** first pricing tier (tierMinimumUnits 0), valid to 50 TB, so all tiers use it.
- **Log Analytics:** priced at the full pay-as-you-go ingestion rate (the tier starting at 5 GB). The API also lists a free first 5 GB at tierMinimumUnits 0; that allowance is per billing account, so it is not credited here. Retention beyond the included 31 days is not priced.
- **SQL long-term backup:** RA-GRS, the default backup redundancy. LRS is half the price (South Africa North $0.0335 vs $0.067 per GB-month).
- **SQL auditing to storage:** has no meter of its own; you pay for the storage account. Estimated as 5 / 10 / 15 GB of Hot LRS audit logs held plus 50k / 100k / 150k write operations a month, giving $0.35 to $1.23 depending on tier and region.
- **ACS Email:** the meters are `Basic Sent Email` ($0.00025 per email) and `Basic Data Transferred` ($0.00012 per MB) under `serviceName eq 'Email'`, the same price in all three regions. 0.5 MB per email as specified.
- **Azure DNS:** public zone, first-25-zones tier ($0.50 per zone-month), listed under region `Zone 1` for every region. Query charges ($0.40 per million) are not included because no volume was given.
- **Key Vault:** Standard-tier `Operations` per 10K. Certificate renewal requests ($3 each) and HSM keys are not included.
- **Service Bus Basic:** priced per million operations, assuming 1M operations a month for every tier. At that volume it is a few cents.
- **Defender for Azure SQL:** `Standard Node`, $15 per server per month, one server per environment.
- **Defender for Storage:** `Standard Node` at $0.0134 per hour x 730 = $9.78, one storage account per environment. The API also lists newer `Defender Workload Unit` meters at $0.006849 per hour ($5.00 a month per unit). I could not confirm from the API how many units one storage account consumes, so I did not use them. Confirm the Defender plan billed on the real subscription before relying on this add-on figure.
- **Malware scanning:** $0.15 per GB scanned, assuming 10% of stored blob GB is scanned each month (5 / 10 / 25 GB). Replace with real upload volume.
- **AI Search:** one unit each, Basic $0.101 per hour and Standard S1 $0.336 per hour, the same in all three regions. Semantic ranker and agentic retrieval are not included.
- **Not priced because they were not in the list:** outbound bandwidth, Application Insights beyond the Log Analytics line, SQL point-in-time backup above the included allowance, public IPs, custom domains and certificates, Front Door or Application Gateway, and any Azure OpenAI or Document Intelligence usage. I do not know what the current estimates ($63.73 / $113.30 / $149.93) include, so part of any difference could be scope rather than price. The App Service and SQL list-price gap alone is $22.81 / $43.12 / $52.39 between South Africa North and West Europe, which accounts for most of the difference to the estimates.
- **Prices change.** The API returns the list price on the day it was queried (2026-10-12).

## 6. How to reproduce

The calculation is a short Python script that queries the Retail Prices API with `$filter`, follows `NextPageLink`, keeps `type eq Consumption` rows, and fails if a lookup returns zero or more than one distinct price. It is not committed to the repository.
