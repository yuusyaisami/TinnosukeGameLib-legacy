# GameLib Kernel v2 Docs

このフォルダには、新しい Kernel 基盤の上位仕様と、その前提を固めるためのレビュー文書を置きます。

- [00 Kernel Architecture Overview Review](00_KernelArchitectureOverviewReview.md)
- [00 Kernel Architecture Overview Specification](00_KernelArchitectureOverviewSpec.md)
- [01 Kernel IR Specification](01_KernelIRSpec.md)

初回の v2 文書では、現行実装の観測結果と移行先の target policy を分離することを最優先にしています。
特に、KernelIR と DependencyValidation を下位仕様の先頭に置く方針を固定します。