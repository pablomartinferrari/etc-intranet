# Lead Inspection — User Guide

**Multifamily lead paint reports (Units & Common Areas)**

This guide walks you through uploading XRF data, reviewing readings, and generating HUD/EPA-style reports for a single job. For setup, troubleshooting, and video production notes, see the [end-to-end runbook](./multifamily-lbp-end-to-end-tutorial.md).

---

## Before you start

You need:

- Access to the SharePoint site with the **Lead Inspection — Upload** web part
- Sign-in to the intranet app (your work Microsoft account)
- XRF Excel (`.xlsx`) or CSV files for the job — typically **one file for Units** and **one for Common Areas**

Use the **same job number** for both uploads. Everything for that job — files, readings, and reports — stays tied to that number.

---

## Workflow at a glance

```text
SharePoint upload  →  Review in intranet  →  [Optional] AI normalize  →  Generate reports  →  Export Excel
```

| Step | Where | What you get |
| ---- | ----- | ------------ |
| 1 | SharePoint | Files stored for the job |
| 2 | Intranet | Readings imported and reviewable |
| 3 | Intranet (optional) | Standardized component names |
| 4 | Intranet | Units report + Common Areas report |
| 5 | Report viewer | Excel workbook per report |

**Note:** Reports work without running AI normalization. Normalization helps when the same component appears under different spellings (for example, "door jamb" vs "Door Jamb").

---

## Step 1 — Upload files in SharePoint

### 1.1 Open the upload page

1. Go to your SharePoint site.
2. Find the **Lead Inspection — Upload** web part.
3. Enter the **job number** (for example, `285744`) and continue.

### 1.2 Upload Units data

1. When asked for area type, choose **Units**.
2. Add your Units `.xlsx` or `.csv` file(s).
3. Click **Upload to SharePoint**.
4. Wait for the success message.

If Units data already exists for this job, choose **Replace existing** or **Add to existing** as appropriate.

### 1.3 Upload Common Areas data

1. Click **Upload more files** (use **Start over** only if you need a different job number).
2. Enter the **same job number** again.
3. Choose **Common Areas**.
4. Upload your Common Areas file(s) and confirm.

You should now have at least two files for the job — one Units dataset and one Common Areas dataset.

### 1.4 Open the intranet app

1. With the job number still active, click **Review Readings**.
2. A new browser tab opens the intranet lead inspection app.
3. Sign in if prompted.

The app automatically imports new SharePoint files when you arrive from **Review Readings**. You land on the **Data grid** if readings exist, or **Source files** if nothing has been imported yet.

---

## Step 2 — Confirm import and review data

### 2.1 Check Source files

In the left sidebar, open **Source files**.

- Confirm your SharePoint files appear in the list.
- If files are listed but no readings were imported, click **Import from SharePoint**.
- A success message shows how many readings were imported.

On **Overview**, check that you see separate counts for **Units rows** and **Common areas rows**.

### 2.2 Review the Data grid

Open **Data grid** in the sidebar.

| Column | What it shows |
| ------ | ------------- |
| **Component** | Normalized name if set; otherwise the name from your file. Edits save to the normalized value. |
| **Substrate** | Normalized value if set; otherwise the imported value (read-only in this grid). |
| **Pb (mg/cm²)** | Lead content; **≥ 1.0** counts as positive |
| **Result** | Positive or Negative badge |

**Tips:**

- Use **Data type** filter to switch between **Units** and **Common Areas**.
- Use **Search** to find specific locations or components.
- After inline edits, click **Save changes**.

---

## Step 3 — AI normalization (optional)

Normalization is **recommended** when component names vary across files, but **not required** to generate reports.

Open **AI normalization** in the sidebar.

### Component normalization (typical first run)

1. **Field:** Component  
2. **Scope:** Entire job (or **Only missing normalized values** on re-runs)  
3. **Data type:** Both (or Units / Common Areas separately)  
4. Click **Run normalization**

### Review suggestions

On the review screen:

- **Approve** — apply the suggestion immediately  
- **Approve all high confidence** — bulk-approve obvious matches  
- Edit a suggested value, then **Update** if already applied  
- **Reject** — keep the original grouping for that suggestion  

Repeat for **Substrate** if needed (run normalization again with **Field = Substrate**).

Open **Grouped readings** to see how components roll up after normalization.

---

## Step 4 — Generate reports

You create **one report per data type** — one for Units, one for Common Areas.

Open **Reports** in the sidebar (Configure report).

### Units report

1. **Data type:** Units  
2. **Sections to include:** leave all checked (All shots, Average, Uniform shots, Non-uniform shots)  
3. Click **Generate report**  
4. The **Report viewer** opens

### Common Areas report

1. Return to **Reports** → Configure report  
2. **Data type:** Common Areas  
3. Click **Generate report**  
4. Review in the report viewer (the header shows data type and generation time)

---

## Step 5 — Understand report tabs

Each report has four tabs. Classification is per **component** (normalized name if present, otherwise the imported name).

| Tab | What appears here |
| --- | ----------------- |
| **All shots** | Every reading in this data type |
| **Average** | Components with **40 or more** readings; POSITIVE if **more than 2.5%** of shots are positive |
| **Uniform** | Fewer than 40 readings, **all** positive or **all** negative |
| **Non-uniform** | Fewer than 40 readings with **mixed** positive and negative results; includes individual shot detail |

### Key thresholds

| Rule | Value |
| ---- | ----- |
| Positive reading | Lead content ≥ **1.0** mg/cm² |
| Statistical sample (Average tab) | **40** readings per component |
| Average positive rule | **> 2.5%** positive → component is POSITIVE |

The **Non-uniform** tab shows a summary row per component (counts and percentages), then a detail table of individual readings below each mixed component.

If you see a warning about an **older report format**, generate a **new** report from Configure report.

---

## Step 6 — Export to Excel

1. Open the report in **Report viewer**.  
2. Click **Export to Excel**.  
3. The workbook includes sheets for All shots, Average, Uniform, and Non-uniform (when data exists for each).

Repeat for both the **Units** and **Common Areas** reports.

---

## Starting over on a job

| Action | Where | What it clears |
| ------ | ----- | -------------- |
| **Clear workspace** | Intranet → Source files or Overview | Imported rows, normalization, and reports for this job |
| **Clear SharePoint files** | SharePoint upload web part | Uploaded files only (not the intranet database) |
| **Start over** | SharePoint upload web part | Resets the upload flow for a new job entry |

To fully reset a job: **Clear workspace** in the intranet, then **Clear SharePoint files** in SharePoint.

---

## Quick reference

**Remember:**

1. One job number — upload **Units** and **Common Areas** separately.  
2. Two reports — generate one per data type.  
3. Normalization is optional; reports use normalized names when set.  
4. Save grid edits with **Save changes**.  
5. Export each report to Excel when done.

---

## Common issues

| Problem | What to try |
| ------- | ----------- |
| **Review Readings** opens blank or 404 | Contact your admin — the SharePoint web part may need the intranet URL configured. |
| Import shows 0 rows | Confirm the job number matches your files; check Source files and retry **Import from SharePoint**. |
| Units report is empty | Generate with **Data type = Units**; confirm Units row count on Overview. |
| Uniform tab is empty | Normal if small component groups are mixed (Non-uniform) or large (Average). |
| Component edits disappear | Click **Save changes** in the data grid. |

For technical setup and admin troubleshooting, see the [end-to-end runbook](./multifamily-lbp-end-to-end-tutorial.md).
