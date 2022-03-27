function scrollIntoView(el)
{
    if(el) el.scrollIntoView({ behavior: "smooth", block: "end" });
}

function getHeight(el)
{
    if(el) return el.offsetHeight;
    return 0;
}

function getWidth(el)
{
    if(el) return el.offsetWidth;
    return 0;
}

function getScrollWidth(el)
{
    if(el) return el.scrollWidth;
    return 0;
}
