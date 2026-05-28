import { Navigate, useParams } from "react-router-dom";
import { MULTIFAMILY_LBP_SLUG } from "@mf/config/entities";

/** Legacy `/job/:id` → `/jobs/:id/multifamily-lbp` */
export function JobRedirectPage(): React.JSX.Element {
  const { jobNumber = "" } = useParams();
  return <Navigate to={`/jobs/${encodeURIComponent(jobNumber)}/${MULTIFAMILY_LBP_SLUG}`} replace />;
}
