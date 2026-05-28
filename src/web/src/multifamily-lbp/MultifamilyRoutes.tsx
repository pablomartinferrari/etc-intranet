import { Navigate, Route, Routes } from "react-router-dom";
import { AppLayout } from "@mf/layout/AppLayout";
import { EntityShellLayout } from "@mf/layout/EntityShellLayout";
import { HomePage } from "@mf/pages/HomePage";
import { JobRedirectPage } from "@mf/pages/JobRedirectPage";
import { EntityDashboardPage } from "@mf/pages/EntityDashboardPage";
import { UploadPage } from "@mf/pages/UploadPage";
import { UploadResultsPage } from "@mf/pages/UploadResultsPage";
import { DataGridPage } from "@mf/pages/DataGridPage";
import { NormalizeSetupPage } from "@mf/pages/NormalizeSetupPage";
import { NormalizeReviewPage } from "@mf/pages/NormalizeReviewPage";
import { ReportConfigurePage } from "@mf/pages/ReportConfigurePage";
import { ReportViewerPage } from "@mf/pages/ReportViewerPage";
import { MultifamilyRedirectPage } from "@mf/pages/MultifamilyRedirectPage";

/** Lead inspection workflow — SharePoint deep links use /jobs/... paths at the app root. */
export default function MultifamilyRoutes() {
  return (
    <Routes>
      <Route path="/lead-inspection" element={<AppLayout />}>
        <Route index element={<HomePage />} />
      </Route>
      <Route path="/jobs/:jobId/:entitySlug" element={<EntityShellLayout />}>
        <Route index element={<EntityDashboardPage />} />
        <Route path="uploads" element={<UploadPage />} />
        <Route path="uploads/results" element={<UploadResultsPage />} />
        <Route path="grid" element={<DataGridPage />} />
        <Route path="normalize" element={<NormalizeSetupPage />} />
        <Route path="normalize/review" element={<NormalizeReviewPage />} />
        <Route path="reports/configure" element={<ReportConfigurePage />} />
        <Route path="reports/viewer" element={<ReportViewerPage />} />
      </Route>
      <Route path="/multifamily/:jobNumber" element={<MultifamilyRedirectPage />} />
      <Route path="/job/:jobNumber" element={<JobRedirectPage />} />
      <Route path="/jobs" element={<Navigate to="/lead-inspection" replace />} />
    </Routes>
  );
}
