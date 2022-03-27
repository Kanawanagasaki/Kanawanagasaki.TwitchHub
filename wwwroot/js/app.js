function scrollIntoView(el)
{
    if(el) el.scrollIntoView({ behavior: "smooth", block: "end" });
}

function getHeight(el)
{
    if(el) return el.offsetHeight;
    return 0;
}
