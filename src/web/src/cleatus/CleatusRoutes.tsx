import { BuildingBankRegular, DataTrendingRegular } from "@fluentui/react-icons";
import { RequireAuth } from "../multifamily-lbp/auth/RequireAuth";
import { CleatusChrome } from "./CleatusChrome";
import { OpportunitiesPage } from "./pages/OpportunitiesPage";
import { PipelinePage } from "./pages/PipelinePage";

export function CleatusOpportunitiesRoute(): React.JSX.Element {
  return (
    <RequireAuth>
      <CleatusChrome
        title="Opportunities"
        subtitle="CLEATUS-recommended SAM.gov and SLED bids"
        icon={<BuildingBankRegular fontSize={28} />}
      >
        <OpportunitiesPage />
      </CleatusChrome>
    </RequireAuth>
  );
}

export function CleatusPipelineRoute(): React.JSX.Element {
  return (
    <RequireAuth>
      <CleatusChrome
        title="Pipeline"
        subtitle="Pursued, won, and lost work — close-out reasons stay in the intranet"
        icon={<DataTrendingRegular fontSize={28} />}
      >
        <PipelinePage />
      </CleatusChrome>
    </RequireAuth>
  );
}
