import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Spinner } from "@/components/ui/spinner";

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
    <AlertDialog
      open={open}
      onOpenChange={(next) => {
        if (!next && !pending) onCancel();
      }}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Clear workspace data?</AlertDialogTitle>
          <AlertDialogDescription>
            This removes all imported readings, normalization suggestions, and generated reports for this job in the
            intranet. SharePoint source files are not deleted — clear those separately in the upload web part if needed.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <p className="text-sm">You can import from SharePoint again afterward.</p>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={pending} onClick={onCancel}>
            Cancel
          </AlertDialogCancel>
          <AlertDialogAction
            disabled={pending}
            onClick={(e) => {
              e.preventDefault();
              onConfirm();
            }}
          >
            {pending ? <Spinner size="sm" /> : "Clear workspace"}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
