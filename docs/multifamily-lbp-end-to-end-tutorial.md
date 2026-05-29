# Multifamily LBP — End-to-end report tutorial

Step-by-step guide for producing HUD/EPA-style lead paint reports for **Units** and **Common Areas** using SharePoint upload and the intranet workspace. Use this as a runbook when recording a video tutorial or walking through the workflow with a new user.

## What you are building

For one **job number**, you will:

1. Upload XRF Excel files to SharePoint (one dataset type at a time: Units or Common Areas).
2. Import those files into the intranet workspace.
3. Optionally run AI normalization to standardize component names.
4. Generate **two reports** — one for Units, one for Common Areas.
5. Review and export each report to Excel.

Reports work **without** normalization. The system uses the normalized component/substrate when set; otherwise it uses the imported values.

---

## Prerequisites

| Item | Notes |
|------|--------|
| SharePoint site | **Lead Inspection — Upload** web part installed |
| Web part setting | **Processing app URL** = intranet URL (no trailing slash), e.g. `https://intranet-yfjgdqq7k75by-api.azurewebsites.net` |
| Intranet app | Deployed with React `wwwroot` + API; user can sign in with Entra ID |
| Graph import (production) | `SharePoint__SiteUrl`, `AzureAd__ClientSecret`, Graph `Sites.Read.All` — see [sharepoint-intranet-workspace.md](../../multifamily-lbp/docs/sharepoint-intranet-workspace.md) in the multifamily-lbp repo |
| Sample files | At least one **Units** workbook and one **Common Areas** workbook for the same job (use demo/synthetic data for recordings) |

---

## Workflow overview

```text
SharePoint (upload)  →  Intranet (import)  →  Review grid  →  [Optional] AI normalize  →  Report × 2  →  Export Excel
```

| Step | Where | Outcome |
|------|--------|---------|
| 1 | SharePoint | Files in `XRF-SourceFiles` library |
| 2 | Intranet → Source files | Rows in database |
| 3 | Intranet → Data grid | Inline edits saved to normalized fields |
| 4 | Intranet → AI normalization | Standardized component/substrate names (optional) |
| 5 | Intranet → Reports | Units report + Common Areas report |
| 6 | Report viewer | Excel export per report |

---

## Part A — SharePoint upload (Units and Common Areas)

### A1. Open the upload web part

1. Go to the SharePoint site with **Lead Inspection — Upload**.
2. Enter the **job number** (e.g. `285744`) and continue.

### A2. Upload Units data

1. When prompted for area type, choose **Units**.
2. Add your Units `.xlsx` or `.csv` file(s).
3. Click **Upload to SharePoint**.
4. Wait for the success message.

If the job already has Units data, choose **Replace existing** or **Add to existing** as appropriate.

### A3. Upload Common Areas data

1. Click **Upload more files** (or **Start over** only if you need a new job number).
2. Enter the **same job number**.
3. When prompted, choose **Common Areas**.
4. Upload the Common Areas file(s) and confirm upload.

You should now have **two files** (or more) in SharePoint for the same job, tagged by area type.

### A4. Open the intranet workspace

1. With the job number still active, click **Review Readings**.
2. A new tab opens:  
   `{intranet}/jobs/{jobNumber}/multifamily-lbp?import=1`
3. Sign in if prompted (Entra ID).

The app imports new SharePoint files automatically on this link, then sends you to the **Data grid** if rows exist, or **Source files** if not.

---

## Part B — Intranet import and review

### B1. Confirm import (Source files)

Navigation: **Source files** in the left sidebar (`/jobs/{jobId}/multifamily-lbp/uploads`).

1. Verify SharePoint files appear in the list.
2. If you landed here without auto-import, click **Import from SharePoint**.
3. Success message shows how many readings were imported.

**Dashboard check:** **Overview** shows separate counts for **Units rows** and **Common areas rows**.

### B2. Review the data grid

Navigation: **Data grid** (`/grid`).

| Column | Behavior |
|--------|----------|
| Component | Shows normalized name if set; otherwise the imported component. Edits save to **normalized** component. |
| Substrate | Shows normalized if set; otherwise original (read-only in flat grid). |
| Pb (mg/cm²) | Lead content; ≥ 1.0 = positive |
| Result | Positive / Negative badge |

Use filters for **Data type** (Units / Common Areas) and **Search** as needed. Click **Save changes** after inline edits.

### B3. Optional — AI normalization

Normalization is **recommended** but **not required** for reports.

Navigation: **AI normalization** (`/normalize`).

**Component normalization (typical first run):**

1. Field: **Component**
2. Scope: **Entire job** (or **Only missing normalized values** on re-runs)
3. Data type: **Both** (or Units / Common Areas separately)
4. Click **Run normalization** → review screen

**Review screen** (`/normalize/review`):

- **Approve** applies the suggestion immediately.
- **Approve all high confidence** for bulk approval.
- Edit the suggested value, then **Update** if already applied.
- **Reject** to keep the original grouping for that suggestion.

Repeat for **Substrate** if needed (run normalization again with Field = Substrate).

**Grouped readings** (`/grid/groups`) shows how components roll up after normalization.

---

## Part C — Generate reports (Units and Common Areas)

You generate **one report per data type**. Each report includes only the readings for that type.

Navigation: **Reports** → **Configure report** (`/reports/configure`).

### C1. Units report

1. **Data type:** **Units**
2. **Sections to include:** leave all checked (All shots, Average, Uniform shots, Non-uniform shots)
3. Click **Generate report**
4. You are taken to the **Report viewer**

### C2. Common Areas report

1. Go back to **Reports** → **Configure report**
2. **Data type:** **Common Areas**
3. Click **Generate report**
4. Review in the report viewer (note the header shows data type and generation time)

---

## Part D — Understanding report tabs

Each report has four tabs. Classification is per **component** (normalized if present, else original), not per component+substrate pair.

| Tab | When a component appears here |
|-----|-------------------------------|
| **All shots** | Every reading in this data type |
| **Average** | 40+ readings for the component → POSITIVE if > 2.5% of shots are positive |
| **Uniform** | Fewer than 40 readings, **all** positive or **all** negative |
| **Non-uniform** | Fewer than 40 readings, **mixed** positive and negative; includes individual shot detail |

### Thresholds (HUD/EPA)

| Rule | Value |
|------|--------|
| Positive reading | Lead content ≥ **1.0** mg/cm² |
| Statistical sample | **40** readings per component |
| Average positive rule | **> 2.5%** positive → component POSITIVE |

### Non-uniform tab

- Summary row: Component, Substrate, Positive Count, Negative Count, Positive %, Total Readings
- Below each mixed component: **individual readings** table (Reading, Substrate, Location, Pb, Positive)

If you see a warning about an **older report format**, generate a **new** report after deploying the latest API.

---

## Part E — Export to Excel

1. Open the report in **Report viewer**
2. Click **Export to Excel**
3. Workbook sheets (when data exists): All shots, Average, Uniform, Non-uniform (with reading detail blocks on Non-uniform)

Repeat export for both the Units and Common Areas reports.

---

## Part F — Reset / start over (demo or re-test)

| Action | Where | What it clears |
|--------|--------|----------------|
| **Clear workspace** | Intranet → Source files or Overview | Database rows, normalization suggestions, reports for this job |
| **Clear SharePoint files** | SharePoint web part | Files in `XRF-SourceFiles` for the job (not intranet DB) |
| **Start over** | SharePoint web part | Resets the upload conversation (new job entry) |

For a clean demo: Clear intranet workspace → Clear SharePoint files → Start over → re-upload.

---

## Video recording checklist

Use this the night before recording:

- [ ] Use **demo/synthetic** inspection data, not real client data
- [ ] Prepare **two Excel files**: one Units, one Common Areas, same job number
- [ ] Include at least one component with **40+ readings** (Average tab)
- [ ] Include components with **all negative** readings (Uniform tab)
- [ ] Include at least one component with **mixed** results under 40 readings (Non-uniform tab)
- [ ] Browser: 1920×1080, notifications off, signed into SharePoint + intranet
- [ ] Confirm web part **Processing app URL** points to the environment you are recording
- [ ] Run through once end-to-end and generate **both** reports before recording

### Suggested video chapters

| Time | Scene |
|------|--------|
| 0:00 | Intro — job number, two dataset types |
| 2:00 | SharePoint upload — Units then Common Areas |
| 5:00 | Review Readings → import → data grid |
| 8:00 | Optional normalization walkthrough |
| 12:00 | Generate Units report — walk through tabs |
| 16:00 | Generate Common Areas report |
| 18:00 | Excel export |
| 20:00 | Wrap-up — when to normalize, reset options |

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Review Readings opens blank / 404 | Check **Processing app URL** on the web part |
| Import returns 0 rows | Confirm Graph/SharePoint settings; verify job number matches file metadata |
| Units report empty | Generate report with **Data type = Units**; confirm Units rows on Overview |
| Uniform tab empty | Expected if all small groups are mixed (Non-uniform) or large (Average) |
| Non-uniform shows wrong columns | Regenerate report (legacy snapshot); deploy latest frontend |
| Component edits revert | Click **Save changes** in data grid; edits target normalized field |

---

## Related docs

| Document | Location |
|----------|----------|
| SharePoint ↔ intranet setup | `multifamily-lbp/docs/sharepoint-intranet-workspace.md` |
| Business rules (40 / 2.5% / non-uniform) | `multifamily-lbp/docs/REQUIREMENTS.md` §4 |
| Video script (narration) | `multifamily-lbp/docs/tutorials/10-end-to-end-units-and-common-areas.md` |
| Intranet local dev | `intranet/README.md` |

---

## URL reference

| Route | Purpose |
|-------|---------|
| `/jobs/{jobId}/multifamily-lbp?import=1` | SharePoint entry + auto-import |
| `/jobs/{jobId}/multifamily-lbp/uploads` | Source files / import |
| `/jobs/{jobId}/multifamily-lbp/grid` | Data grid |
| `/jobs/{jobId}/multifamily-lbp/normalize` | AI normalization |
| `/jobs/{jobId}/multifamily-lbp/reports/configure` | Generate report |
| `/jobs/{jobId}/multifamily-lbp/reports/viewer?reportId={id}` | View / export report |
