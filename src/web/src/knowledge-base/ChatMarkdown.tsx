import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { makeStyles, tokens } from "@fluentui/react-components";
import type { Components } from "react-markdown";

type ChatMarkdownProps = {
  content: string;
  className?: string;
};

export function ChatMarkdown({ content, className }: ChatMarkdownProps) {
  const styles = useStyles();

  const components: Components = {
    a: ({ href, children }) => (
      <a href={href} target="_blank" rel="noopener noreferrer" className={styles.link}>
        {children}
      </a>
    ),
    p: ({ children }) => <p className={styles.paragraph}>{children}</p>,
    ul: ({ children }) => <ul className={styles.list}>{children}</ul>,
    ol: ({ children }) => <ol className={styles.list}>{children}</ol>,
    li: ({ children }) => <li className={styles.listItem}>{children}</li>,
    strong: ({ children }) => <strong className={styles.strong}>{children}</strong>,
    em: ({ children }) => <em className={styles.em}>{children}</em>,
    h1: ({ children }) => <h3 className={styles.heading}>{children}</h3>,
    h2: ({ children }) => <h3 className={styles.heading}>{children}</h3>,
    h3: ({ children }) => <h4 className={styles.subheading}>{children}</h4>,
    blockquote: ({ children }) => <blockquote className={styles.blockquote}>{children}</blockquote>,
    pre: ({ children }) => <pre className={styles.pre}>{children}</pre>,
    code: ({ className: codeClassName, children, ...props }) => {
      const isBlock = codeClassName?.includes("language-");
      return (
        <code
          className={isBlock ? `${styles.codeBlock} ${codeClassName ?? ""}` : styles.inlineCode}
          {...props}
        >
          {children}
        </code>
      );
    },
  };

  return (
    <div className={`${styles.markdown} ${className ?? ""}`}>
      <ReactMarkdown remarkPlugins={[remarkGfm]} components={components}>
        {content}
      </ReactMarkdown>
    </div>
  );
}

const useStyles = makeStyles({
  markdown: {
    fontSize: tokens.fontSizeBase300,
    lineHeight: tokens.lineHeightBase300,
    color: "inherit",
    "& > :first-child": { marginTop: 0 },
    "& > :last-child": { marginBottom: 0 },
  },
  paragraph: {
    marginTop: 0,
    marginBottom: "0.75em",
  },
  list: {
    marginTop: 0,
    marginBottom: "0.75em",
    paddingLeft: "1.25em",
  },
  listItem: {
    marginBottom: "0.35em",
  },
  link: {
    color: tokens.colorBrandForeground1,
    textDecoration: "underline",
    wordBreak: "break-word",
    ":hover": {
      color: tokens.colorBrandForeground2,
    },
  },
  strong: {
    fontWeight: tokens.fontWeightSemibold,
  },
  em: {
    fontStyle: "italic",
  },
  heading: {
    marginTop: "0.5em",
    marginBottom: "0.5em",
    fontSize: tokens.fontSizeBase400,
    fontWeight: tokens.fontWeightSemibold,
  },
  subheading: {
    marginTop: "0.5em",
    marginBottom: "0.5em",
    fontSize: tokens.fontSizeBase300,
    fontWeight: tokens.fontWeightSemibold,
  },
  blockquote: {
    marginTop: 0,
    marginBottom: "0.75em",
    paddingLeft: "12px",
    borderLeft: `3px solid ${tokens.colorNeutralStroke1}`,
    color: tokens.colorNeutralForeground2,
  },
  pre: {
    marginTop: 0,
    marginBottom: "0.75em",
    padding: "12px",
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground3,
    overflowX: "auto",
  },
  codeBlock: {
    fontFamily: tokens.fontFamilyMonospace,
    fontSize: tokens.fontSizeBase200,
    whiteSpace: "pre",
  },
  inlineCode: {
    fontFamily: tokens.fontFamilyMonospace,
    fontSize: "0.92em",
    padding: "1px 5px",
    borderRadius: tokens.borderRadiusSmall,
    backgroundColor: tokens.colorNeutralBackground3,
  },
});
