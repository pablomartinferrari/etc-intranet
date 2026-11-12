import { Navigate, useParams } from "react-router-dom";
import { MULTIFAMILY_LBP_SLUG } from "@mf/config/entities";

/** Legacy `/multifamily/:jobNumber` → entity dashboard */
export function MultifamilyRedirectPage(): React.JSX.Element {
  const { jobNumber = "" } = useParams();
  return <Navigate to={`/jobs/${encodeURIComponent(jobNumber)}/${MULTIFAMILY_LBP_SLUG}`} replace />;
}
