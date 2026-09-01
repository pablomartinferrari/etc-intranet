import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { Components } from "react-markdown";

import { cn } from "@/lib/utils";

type ChatMarkdownProps = {
  content: string;
  className?: string;
};

export function ChatMarkdown({ content, className }: ChatMarkdownProps) {
  const components: Components = {
    a: ({ href, children }) => (
      <a
        href={href}
        target="_blank"
        rel="noopener noreferrer"
        className="break-words text-primary underline hover:opacity-80"
      >
        {children}
      </a>
    ),
    p: ({ children }) => <p className="mb-3 mt-0">{children}</p>,
    ul: ({ children }) => <ul className="mb-3 mt-0 list-disc pl-5">{children}</ul>,
    ol: ({ children }) => <ol className="mb-3 mt-0 list-decimal pl-5">{children}</ol>,
    li: ({ children }) => <li className="mb-1">{children}</li>,
    strong: ({ children }) => <strong className="font-semibold">{children}</strong>,
    em: ({ children }) => <em className="italic">{children}</em>,
    h1: ({ children }) => <h3 className="mb-2 mt-2 text-base font-semibold">{children}</h3>,
    h2: ({ children }) => <h3 className="mb-2 mt-2 text-base font-semibold">{children}</h3>,
    h3: ({ children }) => <h4 className="mb-2 mt-2 text-sm font-semibold">{children}</h4>,
    blockquote: ({ children }) => (
      <blockquote className="mb-3 mt-0 border-l-2 border-border pl-3 text-muted-foreground">
        {children}
      </blockquote>
    ),
    pre: ({ children }) => (
      <pre className="mb-3 mt-0 overflow-x-auto rounded-md bg-muted p-3">{children}</pre>
    ),
    code: ({ className: codeClassName, children, ...props }) => {
      const isBlock = codeClassName?.includes("language-");
      return (
        <code
          className={
            isBlock
              ? cn("font-mono text-xs whitespace-pre", codeClassName)
              : "rounded-sm bg-muted px-1.5 py-px font-mono text-[0.92em]"
          }
          {...props}
        >
          {children}
        </code>
      );
    },
  };

  return (
    <div className={cn("text-sm leading-6 [&>:first-child]:mt-0 [&>:last-child]:mb-0", className)}>
      <ReactMarkdown remarkPlugins={[remarkGfm]} components={components}>
        {content}
      </ReactMarkdown>
    </div>
  );
}
