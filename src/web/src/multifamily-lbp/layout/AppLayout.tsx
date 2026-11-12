import { Outlet } from "react-router-dom";
import { LeadInspectionChrome } from "./LeadInspectionChrome";

export function AppLayout(): React.JSX.Element {
  return (
    <LeadInspectionChrome>
      <Outlet />
    </LeadInspectionChrome>
  );
}
