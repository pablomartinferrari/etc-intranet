import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogSurface,
  DialogTitle,
  Spinner,
  Text,
} from "@fluentui/react-components";

export function ClearWorkspaceDialog({
  open,
  pending,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  pending: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}): React.JSX.Element {
  return (
    <Dialog open={open} onOpenChange={(_, d) => !d.open && !pending && onCancel()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Clear workspace data?</DialogTitle>
          <Text block>
            This removes all imported readings, normalization suggestions, and generated reports for this job in the
            intranet. SharePoint source files are not deleted — clear those separately in the upload web part if needed.
          </Text>
          <Text block style={{ marginTop: 8 }}>
            You can import from SharePoint again afterward.
          </Text>
        </DialogBody>
        <DialogActions>
          <Button appearance="secondary" disabled={pending} onClick={onCancel}>
            Cancel
          </Button>
          <Button appearance="primary" disabled={pending} onClick={onConfirm}>
            {pending ? <Spinner size="tiny" /> : "Clear workspace"}
          </Button>
        </DialogActions>
      </DialogSurface>
    </Dialog>
  );
}
